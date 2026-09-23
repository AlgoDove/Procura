using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Procura.API.AI.Agents.VendorEvaluation;
using Procura.API.AI.Agents.VendorEvaluation.Tools;
using Procura.API.AI.Core;
using Procura.API.AI.Gemini;
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Services;
using Xunit;
using Xunit.Abstractions;

namespace Procura.API.Tests.AI.Agents.VendorEvaluation
{
    /// <summary>
    /// Manual smoke tests that call the REAL Gemini API.
    /// Requires "Gemini:ApiKey" to be set in .NET User Secrets:
    ///   dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY"
    /// Run individually with:
    ///   dotnet test --filter "VendorEvaluationAgent_ManualSmokeTest"
    /// </summary>
    public class VendorEvaluationAgent_ManualSmokeTest
    {
        private readonly ITestOutputHelper _output;

        public VendorEvaluationAgent_ManualSmokeTest(ITestOutputHelper output)
        {
            _output = output;
        }

        // ─── Shared Setup ────────────────────────────────────────────────────────

        private static GeminiClient BuildRealGeminiClient()
        {
            var config = new ConfigurationBuilder()
                .AddUserSecrets<VendorEvaluationAgent_ManualSmokeTest>()
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var options = Options.Create(new GeminiOptions
            {
                ApiKey = config["Gemini:ApiKey"],
                Model  = config["Gemini:Model"] ?? "gemini-3.5-flash",
                TimeoutSeconds = 30,
                MaxRetries = 1
            });

            return new GeminiClient(new HttpClient(), options, NullLogger<GeminiClient>.Instance);
        }

        private static (VendorEvaluationAgent agent, Mock<IVendorEvaluationService> serviceMock)
            BuildAgent(Guid requestId, int totalCandidatesEvaluated = 2)
        {
            var serviceMock = new Mock<IVendorEvaluationService>();

            // Setup: ScoreVendorsTool path
            serviceMock
                .Setup(s => s.EvaluateAndRankCandidateVendorsAsync(
                    It.IsAny<EvaluateVendorsRequestDto>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new ProcurementEvaluationSummaryDto
                {
                    ProcurementRequestId    = requestId,
                    TotalCandidatesEvaluated = totalCandidatesEvaluated,
                    TopRecommendedVendorId  = Guid.NewGuid(),
                    TopScore                = 88.5m,
                    RecommendationSummary   = "Vendor A is the top recommendation with a score of 88.5 — best price-to-delivery ratio."
                });

            // Setup: GenerateRecommendationTool path
            serviceMock
                .Setup(s => s.GetRecommendationSummaryAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new ProcurementEvaluationSummaryDto
                {
                    ProcurementRequestId    = requestId,
                    TotalCandidatesEvaluated = totalCandidatesEvaluated,
                    TopRecommendedVendorId  = Guid.NewGuid(),
                    TopScore                = 88.5m,
                    RecommendationSummary   = "Vendor A is the top recommendation with a score of 88.5."
                });

            // Register BOTH tools so neither intent causes a security violation
            var scoreTool          = new ScoreVendorsTool(serviceMock.Object);
            var recommendationTool = new GenerateRecommendationTool(serviceMock.Object);
            var registry  = new ToolRegistry(new IAgentTool[] { scoreTool, recommendationTool });
            var validator = new VendorEvaluationDeterministicValidator();
            var logger    = new Mock<ILogger<VendorEvaluationAgent>>();

            var agent = new VendorEvaluationAgent(
                BuildRealGeminiClient(), registry, validator, logger.Object);

            return (agent, serviceMock);
        }

        // ─── Smoke Test 1: SCORE_VENDORS intent → expects COMPLETED ──────────────

        [Fact(Skip = "Manual smoke test requiring live Gemini API key and quota.")]
        public async Task SmokeTest_ScoreVendors_RealGeminiCall_ReturnsCompleted()
        {
            var requestId = Guid.NewGuid();
            var (agent, _) = BuildAgent(requestId, totalCandidatesEvaluated: 2);

            var context = new WorkflowContext
            {
                WorkflowId          = Guid.NewGuid(),
                RequesterId         = Guid.NewGuid(),
                RequesterRole       = "PROCUREMENT_OFFICER",
                ProcurementRequestId = requestId,   // ← set on context as fallback
                Objective = $"Please evaluate and rank all vendors for procurement request {requestId}. " +
                            $"Weight price at 50% and delivery time at 30% and reliability at 20%."
            };

            var result = await agent.ExecuteAsync(context);

            _output.WriteLine($"Status   : {result.Status}");
            _output.WriteLine($"Summary  : {result.ExecutionSummary}");
            _output.WriteLine($"Clarify  : {result.ClarificationPrompt ?? "(none)"}");
            _output.WriteLine($"Audit entries: {context.AuditTrail.Count}");

            Assert.Equal(AgentExecutionStatus.COMPLETED, result.Status);
        }

        // ─── Smoke Test 2: GET_RECOMMENDATION intent → expects COMPLETED ─────────

        [Fact(Skip = "Manual smoke test requiring live Gemini API key and quota.")]
        public async Task SmokeTest_GetRecommendation_RealGeminiCall_ReturnsCompleted()
        {
            var requestId = Guid.NewGuid();
            var (agent, _) = BuildAgent(requestId, totalCandidatesEvaluated: 2);

            var context = new WorkflowContext
            {
                WorkflowId           = Guid.NewGuid(),
                RequesterId          = Guid.NewGuid(),
                RequesterRole        = "PROCUREMENT_OFFICER",
                ProcurementRequestId  = requestId,   // ← set on context as fallback
                Objective = $"Show me the existing vendor evaluation results for procurement request {requestId}."
            };

            var result = await agent.ExecuteAsync(context);

            _output.WriteLine($"Status   : {result.Status}");
            _output.WriteLine($"Summary  : {result.ExecutionSummary}");
            _output.WriteLine($"Clarify  : {result.ClarificationPrompt ?? "(none)"}");

            Assert.Equal(AgentExecutionStatus.COMPLETED, result.Status);
        }

        // ─── Smoke Test 3: Missing RequestId → expects NEEDS_USER_INPUT ──────────

        [Fact(Skip = "Manual smoke test requiring live Gemini API key and quota.")]
        public async Task SmokeTest_MissingRequestId_RealGeminiCall_ReturnsNeedsUserInput()
        {
            var requestId = Guid.NewGuid();
            var (agent, _) = BuildAgent(requestId);

            var context = new WorkflowContext
            {
                WorkflowId    = Guid.NewGuid(),
                RequesterId   = Guid.NewGuid(),
                RequesterRole = "PROCUREMENT_OFFICER",
                // No ProcurementRequestId set — no GUID in objective either
                Objective     = "Please evaluate all the vendors."
            };

            var result = await agent.ExecuteAsync(context);

            _output.WriteLine($"Status   : {result.Status}");
            _output.WriteLine($"Summary  : {result.ExecutionSummary}");
            _output.WriteLine($"Clarify  : {result.ClarificationPrompt ?? "(none)"}");

            Assert.Equal(AgentExecutionStatus.NEEDS_USER_INPUT, result.Status);
        }

        // ─── Smoke Test 4: No quotes in DB → expects NEEDS_USER_INPUT ────────────

        [Fact(Skip = "Manual smoke test requiring live Gemini API key and quota.")]
        public async Task SmokeTest_NoQuotesInDatabase_RealGeminiCall_ReturnsNeedsUserInput()
        {
            var requestId = Guid.NewGuid();
            // TotalCandidatesEvaluated = 0 simulates an empty quotes table
            var (agent, _) = BuildAgent(requestId, totalCandidatesEvaluated: 0);

            var context = new WorkflowContext
            {
                WorkflowId           = Guid.NewGuid(),
                RequesterId          = Guid.NewGuid(),
                RequesterRole        = "PROCUREMENT_OFFICER",
                ProcurementRequestId  = requestId,
                Objective = $"Evaluate vendors for procurement request {requestId}."
            };

            var result = await agent.ExecuteAsync(context);

            _output.WriteLine($"Status   : {result.Status}");
            _output.WriteLine($"Summary  : {result.ExecutionSummary}");
            _output.WriteLine($"Clarify  : {result.ClarificationPrompt ?? "(none)"}");

            Assert.Equal(AgentExecutionStatus.NEEDS_USER_INPUT, result.Status);
        }
    }
}