using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Procura.API.AI.Agents.VendorManagement;
using Procura.API.AI.Agents.VendorManagement.Tools;
using Procura.API.AI.Core;
using Procura.API.AI.Gemini;
using Xunit;

namespace Procura.API.Tests.AI.VendorManagement
{
    // A tiny fake tool used only in these tests, so ToolRegistry has something
    // real to call without needing a database or the actual SearchVendorsTool/SelectVendorTool.
    public class FakeSearchVendorsTool : IAgentTool
    {
        public string Name => "SearchVendors";
        public string Description => "fake";
        public bool IsMutating => false;
        public List<VendorCandidate> ResultToReturn { get; set; } = new();

        public Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            return Task.FromResult(ToolResult.Ok(Name, ResultToReturn));
        }
    }

    public class FakeSelectVendorTool : IAgentTool
    {
        public string Name => "SelectVendor";
        public string Description => "fake";
        public bool IsMutating => true;
        public bool WasCalled { get; private set; }

        public Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(ToolResult.Ok(Name, new SelectVendorToolInput()));
        }
    }

    public class VendorManagementAgentTests
    {
        private readonly Mock<IGeminiClient> _geminiMock;
        private readonly FakeSearchVendorsTool _searchTool;
        private readonly FakeSelectVendorTool _selectTool;
        private readonly ToolRegistry _toolRegistry;
        private readonly VendorManagementDeterministicValidator _validator;
        private readonly VendorManagementAgent _agent;

        public VendorManagementAgentTests()
        {
            _geminiMock = new Mock<IGeminiClient>();
            _searchTool = new FakeSearchVendorsTool();
            _selectTool = new FakeSelectVendorTool();
            _toolRegistry = new ToolRegistry(new List<IAgentTool> { _searchTool, _selectTool });
            _validator = new VendorManagementDeterministicValidator();

            _agent = new VendorManagementAgent(
                _geminiMock.Object,
                _toolRegistry,
                _validator,
                new LoggerFactory().CreateLogger<VendorManagementAgent>());
        }

        private static WorkflowContext MakeContext(Guid? procurementRequestId, string objective = "Find vendors for office supplies")
        {
            return new WorkflowContext
            {
                Objective = objective,
                RequesterId = Guid.NewGuid(),
                RequesterRole = "PROCUREMENT_OFFICER",
                ProcurementRequestId = procurementRequestId,
                RequestNumber = procurementRequestId.HasValue ? "PR-2026-00001" : null
            };
        }

        // VM-AI-01: agent refuses to run without a ProcurementRequestId already on the context
        [Fact]
        public async Task ExecuteAsync_WithoutProcurementRequestId_ReturnsFailed()
        {
            var context = MakeContext(procurementRequestId: null);

            var result = await _agent.ExecuteAsync(context);

            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
        }

        // VM-AI-02: Gemini extracts an empty category -> NEEDS_USER_INPUT
        [Fact]
        public async Task ExecuteAsync_WithEmptyCategory_ReturnsNeedsUserInput()
        {
            var json = "{\"category\":\"\",\"hasSufficientInformation\":false,\"missingInformationReasons\":[\"No category mentioned\"]}";
            _geminiMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(json));

            var context = MakeContext(procurementRequestId: Guid.NewGuid());

            var result = await _agent.ExecuteAsync(context);

            Assert.Equal(AgentExecutionStatus.NEEDS_USER_INPUT, result.Status);
            Assert.NotNull(result.ClarificationPrompt);
        }

        // VM-AI-03: valid category, no matching vendors -> COMPLETED with zero candidates (not a failure)
        [Fact]
        public async Task ExecuteAsync_WithNoCandidates_ReturnsCompletedWithEmptyList()
        {
            var json = "{\"category\":\"Office Supplies\",\"hasSufficientInformation\":true}";
            _geminiMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(json));

            _searchTool.ResultToReturn = new List<VendorCandidate>();

            var context = MakeContext(procurementRequestId: Guid.NewGuid());

            var result = await _agent.ExecuteAsync(context);

            Assert.Equal(AgentExecutionStatus.COMPLETED, result.Status);
            var output = Assert.IsType<VendorSelectionOutput>(result.OutputData);
            Assert.Empty(output.Candidates);
            Assert.False(_selectTool.WasCalled);
        }

        // VM-AI-04: valid category, candidates found -> COMPLETED, highest-rated vendor selected
        [Fact]
        public async Task ExecuteAsync_WithCandidates_SelectsHighestRatedAndCompletes()
        {
            var json = "{\"category\":\"Office Supplies\",\"hasSufficientInformation\":true}";
            _geminiMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(json));

            var bestVendorId = Guid.NewGuid();
            _searchTool.ResultToReturn = new List<VendorCandidate>
            {
                new VendorCandidate { VendorId = Guid.NewGuid(), Name = "Low Rated Co", Rating = 2.0m },
                new VendorCandidate { VendorId = bestVendorId, Name = "Best Vendor Co", Rating = 4.8m }
            };

            var context = MakeContext(procurementRequestId: Guid.NewGuid());

            var result = await _agent.ExecuteAsync(context);

            Assert.Equal(AgentExecutionStatus.COMPLETED, result.Status);
            var output = Assert.IsType<VendorSelectionOutput>(result.OutputData);
            Assert.Equal(bestVendorId, output.SelectedVendorId);
            Assert.True(_selectTool.WasCalled);
        }

        // VM-AI-05: an unregistered tool name is rejected and recorded as a security violation
        [Fact]
        public async Task ToolRegistry_WithUnregisteredToolName_IsBlockedAndSecurityFlagged()
        {
            var toolContext = new ToolExecutionContext { WorkflowId = Guid.NewGuid() };

            var result = await _toolRegistry.ExecuteToolAsync("SomeToolThatWasNeverRegistered", "input", toolContext);

            Assert.False(result.Success);
            Assert.True(result.IsSecurityViolation);
        }
    }
}