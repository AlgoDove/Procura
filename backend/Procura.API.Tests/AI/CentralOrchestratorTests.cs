using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Procura.API.AI.Agents.ProcurementRequest;
using Procura.API.AI.Agents.VendorManagement;
using Procura.API.AI.Core;
using Procura.API.AI.Entities;
using Procura.API.AI.Orchestration;
using Procura.API.AI.Persistence;
using Xunit;

namespace Procura.API.Tests.AI
{
    public class CentralOrchestratorTests
    {
        private readonly Mock<IWorkflowRepository> _workflowRepoMock;
        private readonly Mock<IProcurementRequestAgent> _agentMock;
        private readonly Mock<IVendorManagementAgent> _vendorAgentMock;
        private readonly Mock<ILogger<CentralOrchestrator>> _loggerMock;
        private readonly CentralOrchestrator _orchestrator;

        public CentralOrchestratorTests()
        {
            _workflowRepoMock = new Mock<IWorkflowRepository>();
            _agentMock = new Mock<IProcurementRequestAgent>();
            _vendorAgentMock = new Mock<IVendorManagementAgent>();
            _loggerMock = new Mock<ILogger<CentralOrchestrator>>();

            _agentMock.Setup(a => a.AgentName).Returns("ProcurementRequestAgent");
            _agentMock.Setup(a => a.Stage).Returns(WorkflowStage.PROCUREMENT_REQUEST);

            _vendorAgentMock.Setup(a => a.AgentName).Returns("VendorManagementAgent");
            _vendorAgentMock.Setup(a => a.Stage).Returns(WorkflowStage.VENDOR_SELECTION);

            _orchestrator = new CentralOrchestrator(
                _workflowRepoMock.Object,
                _agentMock.Object,
                _vendorAgentMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task ProcessWorkflowAsync_InitializesWorkflowId_CreatesPlan_AndDelegatesToAgent()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var objective = "Need 10 chairs for conference room";

            var agentResult = new AgentResult
            {
                Status = AgentExecutionStatus.COMPLETED,
                ProcurementRequestId = Guid.NewGuid(),
                RequestNumber = "PR-2026-00005",
                EstimatedTotal = 150000,
                ExecutionSummary = "Created draft successfully."
            };

            _agentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(agentResult);

            // Act
            var context = await _orchestrator.ProcessWorkflowAsync(objective, requesterId, "EMPLOYEE");

            // Assert
            Assert.NotEqual(Guid.Empty, context.WorkflowId);
            Assert.Equal(objective, context.Objective);
            Assert.Equal(requesterId, context.RequesterId);

            // Verify 4-step plan exists
            Assert.NotNull(context.Plan);
            Assert.Equal(4, context.Plan.Steps.Count);
            Assert.Equal(StepStatus.COMPLETED, context.Plan.Steps[0].Status);
            Assert.Equal(StepStatus.NOT_STARTED, context.Plan.Steps[1].Status);
            Assert.Equal(StepStatus.NOT_STARTED, context.Plan.Steps[2].Status);
            Assert.Equal(StepStatus.NOT_STARTED, context.Plan.Steps[3].Status);

            // Verify stage completion & handoff
            Assert.Equal(WorkflowStatus.STAGE_COMPLETED, context.Status);
            Assert.Equal(WorkflowStage.VENDOR_SELECTION, context.CurrentStage);
            Assert.Equal("PR-2026-00005", context.RequestNumber);

            // Verify delegation occurred
            _agentMock.Verify(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()), Times.Once);

            // Verify state persisted
            _workflowRepoMock.Verify(r => r.AddAsync(It.IsAny<WorkflowInstance>()), Times.Once);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);

            // Verify audit trajectory
            Assert.Contains(context.AuditTrail, a => a.Action == "WORKFLOW_CREATED");
            Assert.Contains(context.AuditTrail, a => a.Action == "PLAN_CREATED");
            Assert.Contains(context.AuditTrail, a => a.Action == "AGENT_DELEGATED");
            Assert.Contains(context.AuditTrail, a => a.Action == "WORKFLOW_COMPLETED");
        }

        [Fact]
        public async Task ProcessWorkflowAsync_WhenAgentRequiresUserInput_PausesWorkflowSafely()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var objective = "I need some items";

            var agentResult = new AgentResult
            {
                Status = AgentExecutionStatus.NEEDS_USER_INPUT,
                ClarificationPrompt = "Please specify item names and quantities.",
                ExecutionSummary = "Details missing.",
                MissingFields = new List<string> { "Items", "Quantities" }
            };

            _agentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(agentResult);

            // Act
            var context = await _orchestrator.ProcessWorkflowAsync(objective, requesterId, "EMPLOYEE");

            // Assert
            Assert.Equal(WorkflowStatus.NEEDS_USER_INPUT, context.Status);
            Assert.Equal("Please specify item names and quantities.", context.ClarificationPrompt);
            Assert.Equal(StepStatus.PENDING, context.Plan.Steps[0].Status);

            // Audit records pause
            Assert.Contains(context.AuditTrail, a => a.Action == "WORKFLOW_PAUSED");

            // State persisted with paused status
            _workflowRepoMock.Verify(r => r.AddAsync(It.Is<WorkflowInstance>(w => w.Status == "NEEDS_USER_INPUT")), Times.Once);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ProcessWorkflowAsync_WhenAgentFails_RecordsFailureAndHalts()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var objective = "Invalid request";

            var agentResult = new AgentResult
            {
                Status = AgentExecutionStatus.FAILED,
                ExecutionSummary = "Validation failed for price.",
                ErrorMessages = new List<string> { "Price cannot be negative." }
            };

            _agentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(agentResult);

            // Act
            var context = await _orchestrator.ProcessWorkflowAsync(objective, requesterId, "EMPLOYEE");

            // Assert
            Assert.Equal(WorkflowStatus.FAILED, context.Status);
            Assert.Equal(StepStatus.FAILED, context.Plan.Steps[0].Status);
            Assert.Contains("Price cannot be negative.", context.Errors);
            Assert.Contains(context.AuditTrail, a => a.Action == "WORKFLOW_FAILED");

            _workflowRepoMock.Verify(r => r.AddAsync(It.Is<WorkflowInstance>(w => w.Status == "FAILED")), Times.Once);
        }

        [Fact]
        public async Task ProcessWorkflowAsync_UnauthorizedUserAccessingWorkflow_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var workflowId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var attackerId = Guid.NewGuid();

            var existingInstance = new WorkflowInstance
            {
                Id = workflowId,
                RequesterId = ownerId,
                RequesterRole = "EMPLOYEE",
                Objective = "Confidential items"
            };

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflowId)).ReturnsAsync(existingInstance);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _orchestrator.ProcessWorkflowAsync("Hacked", attackerId, "EMPLOYEE", workflowId: workflowId));
        }

        [Fact]
        public async Task AuditTrail_CapturesTrajectoryWithTimestampsAndActors()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var objective = "Need laptops";

            var agentResult = new AgentResult
            {
                Status = AgentExecutionStatus.COMPLETED,
                ProcurementRequestId = Guid.NewGuid(),
                RequestNumber = "PR-2026-00009",
                ExecutionSummary = "Completed."
            };

            _agentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(agentResult);

            // Act
            var context = await _orchestrator.ProcessWorkflowAsync(objective, requesterId, "EMPLOYEE");

            // Assert
            Assert.NotEmpty(context.AuditTrail);
            Assert.All(context.AuditTrail, entry =>
            {
                Assert.NotEqual(default, entry.Timestamp);
                Assert.False(string.IsNullOrWhiteSpace(entry.Actor));
                Assert.False(string.IsNullOrWhiteSpace(entry.Action));
            });
        }

        [Fact]
        public async Task ProcessWorkflowAsync_WhenStageIsVendorSelection_DelegatesToVendorAgent_AndTransitionsToEvaluation()
        {
            // Arrange
            var workflowId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();
            var existingRequestId = Guid.NewGuid();
            var objective = "Find office furniture vendors";

            var existingContext = new WorkflowContext
            {
                WorkflowId = workflowId,
                RequesterId = requesterId,
                RequesterRole = "EMPLOYEE",
                CurrentStage = WorkflowStage.VENDOR_SELECTION,
                Status = WorkflowStatus.STAGE_COMPLETED,
                ProcurementRequestId = existingRequestId,
                RequestNumber = "PR-2026-00010"
            };

            var contextJson = System.Text.Json.JsonSerializer.Serialize(existingContext);
            var existingInstance = new WorkflowInstance
            {
                Id = workflowId,
                RequesterId = requesterId,
                ContextJson = contextJson
            };

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflowId)).ReturnsAsync(existingInstance);

            var vendorResult = new AgentResult
            {
                Status = AgentExecutionStatus.COMPLETED,
                ExecutionSummary = "Found and selected candidate vendor.",
                ProcurementRequestId = existingRequestId,
                RequestNumber = "PR-2026-00010"
            };

            _vendorAgentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(vendorResult);

            // Act
            var resultContext = await _orchestrator.ProcessWorkflowAsync(
                objective,
                requesterId,
                "EMPLOYEE",
                workflowId: workflowId);

            // Assert
            Assert.Equal(WorkflowStage.VENDOR_EVALUATION, resultContext.CurrentStage);
            Assert.Equal(WorkflowStatus.STAGE_COMPLETED, resultContext.Status);
            Assert.Equal(StepStatus.COMPLETED, resultContext.Plan.Steps[1].Status);
            _vendorAgentMock.Verify(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ProcessWorkflowAsync_WhenVendorAgentNeedsUserInput_PausesWorkflowSafely()
        {
            // Arrange
            var workflowId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var existingContext = new WorkflowContext
            {
                WorkflowId = workflowId,
                RequesterId = requesterId,
                RequesterRole = "EMPLOYEE",
                CurrentStage = WorkflowStage.VENDOR_SELECTION,
                Status = WorkflowStatus.STAGE_COMPLETED,
                ProcurementRequestId = Guid.NewGuid()
            };

            var contextJson = System.Text.Json.JsonSerializer.Serialize(existingContext);
            var existingInstance = new WorkflowInstance
            {
                Id = workflowId,
                RequesterId = requesterId,
                ContextJson = contextJson
            };

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflowId)).ReturnsAsync(existingInstance);

            var vendorResult = new AgentResult
            {
                Status = AgentExecutionStatus.NEEDS_USER_INPUT,
                ClarificationPrompt = "Which vendor category is required?",
                ExecutionSummary = "Waiting for user clarification."
            };

            _vendorAgentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(vendorResult);

            // Act
            var resultContext = await _orchestrator.ProcessWorkflowAsync(
                "Find vendors",
                requesterId,
                "EMPLOYEE",
                workflowId: workflowId);

            // Assert
            Assert.Equal(WorkflowStatus.NEEDS_USER_INPUT, resultContext.Status);
            Assert.Equal(WorkflowStage.VENDOR_SELECTION, resultContext.CurrentStage);
            Assert.Equal("Which vendor category is required?", resultContext.ClarificationPrompt);
            Assert.Equal(StepStatus.PENDING, resultContext.Plan.Steps[1].Status);
        }

        [Fact]
        public async Task ProcessWorkflowAsync_WhenVendorAgentFails_RecordsFailureAndHalts()
        {
            // Arrange
            var workflowId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var existingContext = new WorkflowContext
            {
                WorkflowId = workflowId,
                RequesterId = requesterId,
                RequesterRole = "EMPLOYEE",
                CurrentStage = WorkflowStage.VENDOR_SELECTION,
                Status = WorkflowStatus.STAGE_COMPLETED,
                ProcurementRequestId = Guid.NewGuid()
            };

            var contextJson = System.Text.Json.JsonSerializer.Serialize(existingContext);
            var existingInstance = new WorkflowInstance
            {
                Id = workflowId,
                RequesterId = requesterId,
                ContextJson = contextJson
            };

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflowId)).ReturnsAsync(existingInstance);

            var vendorResult = new AgentResult
            {
                Status = AgentExecutionStatus.FAILED,
                ExecutionSummary = "SearchVendors tool threw an exception.",
                ErrorMessages = new List<string> { "Database error occurred." }
            };

            _vendorAgentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(vendorResult);

            // Act
            var resultContext = await _orchestrator.ProcessWorkflowAsync(
                "Find vendors",
                requesterId,
                "EMPLOYEE",
                workflowId: workflowId);

            // Assert
            Assert.Equal(WorkflowStatus.FAILED, resultContext.Status);
            Assert.Equal(WorkflowStage.VENDOR_SELECTION, resultContext.CurrentStage);
            Assert.Contains("Database error occurred.", resultContext.Errors);
            Assert.Equal(StepStatus.FAILED, resultContext.Plan.Steps[1].Status);
        }

        [Fact]
        public async Task ProcessWorkflowAsync_IntegratedProgression_FromStage1Completion_ToStage2Delegation()
        {
            // Arrange
            // Simulate a workflow where Stage 1 (Procurement Request) completes,
            // immediately advancing into Stage 2 (Vendor Selection) in subsequent cycle or call.
            var workflowId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();
            var requestId = Guid.NewGuid();

            // First call: Stage 1 completes
            var prResult = new AgentResult
            {
                Status = AgentExecutionStatus.COMPLETED,
                ProcurementRequestId = requestId,
                RequestNumber = "PR-2026-00099",
                EstimatedTotal = 500000,
                ExecutionSummary = "Created draft procurement request."
            };

            _agentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(prResult);

            var stage1Context = await _orchestrator.ProcessWorkflowAsync("Need 5 laptops", requesterId, "EMPLOYEE", workflowId: workflowId);

            Assert.Equal(WorkflowStage.VENDOR_SELECTION, stage1Context.CurrentStage);
            Assert.Equal(WorkflowStatus.STAGE_COMPLETED, stage1Context.Status);
            Assert.Equal(StepStatus.COMPLETED, stage1Context.Plan.Steps[0].Status);

            // Second call: With the updated context persisted, user triggers Stage 2
            var stage1Json = System.Text.Json.JsonSerializer.Serialize(stage1Context);
            var savedInstance = new WorkflowInstance
            {
                Id = workflowId,
                RequesterId = requesterId,
                ContextJson = stage1Json
            };
            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflowId)).ReturnsAsync(savedInstance);

            var vendorResult = new AgentResult
            {
                Status = AgentExecutionStatus.COMPLETED,
                ExecutionSummary = "Selected vendor Acme Corp.",
                ProcurementRequestId = requestId,
                RequestNumber = "PR-2026-00099"
            };

            _vendorAgentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(vendorResult);

            // Act
            var stage2Context = await _orchestrator.ProcessWorkflowAsync("Find vendors for laptops", requesterId, "EMPLOYEE", workflowId: workflowId);

            // Assert
            Assert.Equal(WorkflowStage.VENDOR_EVALUATION, stage2Context.CurrentStage);
            Assert.Equal(WorkflowStatus.STAGE_COMPLETED, stage2Context.Status);
            Assert.Equal(StepStatus.COMPLETED, stage2Context.Plan.Steps[1].Status);
            Assert.Equal(StepStatus.NOT_STARTED, stage2Context.Plan.Steps[2].Status);
        }
    }
}
