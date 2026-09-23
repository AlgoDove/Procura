using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Procura.API.AI.Agents.VendorEvaluation;
using Procura.API.AI.Agents.VendorEvaluation.Tools;
using Procura.API.AI.Core;
using Procura.API.AI.Gemini;
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Services;
using Xunit;

namespace Procura.API.Tests.AI.Agents.VendorEvaluation
{
    public class VendorEvaluationAgentTests
    {
        private readonly Mock<IGeminiClient> _mockGeminiClient;
        private readonly Mock<IVendorEvaluationService> _mockEvaluationService;
        private readonly Mock<ILogger<VendorEvaluationAgent>> _mockLogger;
        private readonly VendorEvaluationDeterministicValidator _validator;

        public VendorEvaluationAgentTests()
        {
            _mockGeminiClient = new Mock<IGeminiClient>();
            _mockEvaluationService = new Mock<IVendorEvaluationService>();
            _mockLogger = new Mock<ILogger<VendorEvaluationAgent>>();
            _validator = new VendorEvaluationDeterministicValidator();
        }

        private (VendorEvaluationAgent agent, ToolRegistry registry) CreateAgentWithTools(IEnumerable<IAgentTool> tools)
        {
            var registry = new ToolRegistry(tools);
            var agent = new VendorEvaluationAgent(_mockGeminiClient.Object, registry, _validator, _mockLogger.Object);
            return (agent, registry);
        }

        [Fact]
        public async Task ExecuteAsync_ScoreVendorsIntent_ReturnsCompletedStatus()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var geminiJson = $@"{{
                ""Action"": ""SCORE_VENDORS"",
                ""ProcurementRequestId"": ""{requestId}"",
                ""CandidateVendors"": [
                    {{
                        ""VendorId"": ""{vendorId}"",
                        ""VendorName"": ""Vendor Alpha"",
                        ""QuotedPrice"": 5000.0,
                        ""EstimatedDeliveryDays"": 7,
                        ""ReliabilityRating"": 95.0,
                        ""IsComplianceApproved"": true
                    }}
                ]
            }}";

            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(geminiJson));

            var scoreTool = new ScoreVendorsTool(_mockEvaluationService.Object);
            var genTool = new GenerateRecommendationTool(_mockEvaluationService.Object);
            var (agent, _) = CreateAgentWithTools(new IAgentTool[] { scoreTool, genTool });

            var expectedSummary = new ProcurementEvaluationSummaryDto
            {
                ProcurementRequestId = requestId,
                TotalCandidatesEvaluated = 1,
                TopRecommendedVendorId = vendorId,
                TopScore = 92.5m,
                RecommendationSummary = $"Top recommended candidate is Vendor {vendorId} (Rank #1) with score 92.50/100.",
                EvaluatedAt = DateTime.UtcNow
            };

            _mockEvaluationService.Setup(s => s.EvaluateAndRankCandidateVendorsAsync(It.IsAny<EvaluateVendorsRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedSummary);

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                RequesterRole = "PROCUREMENT_OFFICER",
                Objective = $"Evaluate vendors for request {requestId}"
            };

            // Act
            var result = await agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.COMPLETED, result.Status);
            Assert.Equal(requestId, result.ProcurementRequestId);
            Assert.Contains("Top recommended candidate", result.ExecutionSummary);
            Assert.NotNull(result.OutputData);
            Assert.Contains(context.AuditTrail, a => a.ToolName == "ScoreVendorsTool");
        }

        [Fact]
        public async Task ExecuteAsync_GetRecommendationIntent_RoutesToGenerateRecommendationTool_ReturnsCompletedStatus()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var geminiJson = $@"{{
                ""Action"": ""GET_RECOMMENDATION"",
                ""ProcurementRequestId"": ""{requestId}""
            }}";

            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(geminiJson));

            var recommendationTool = new GenerateRecommendationTool(_mockEvaluationService.Object);
            var (agent, _) = CreateAgentWithTools(new IAgentTool[] { recommendationTool });

            var expectedSummary = new ProcurementEvaluationSummaryDto
            {
                ProcurementRequestId = requestId,
                TotalCandidatesEvaluated = 2,
                TopRecommendedVendorId = vendorId,
                TopScore = 88.0m,
                RecommendationSummary = $"Top recommended candidate is Vendor {vendorId} (Rank #1) with score 88.00/100.",
                EvaluatedAt = DateTime.UtcNow
            };

            _mockEvaluationService.Setup(s => s.GetRecommendationSummaryAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedSummary);

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                RequesterRole = "MANAGER",
                Objective = $"Show me the current recommendation for request {requestId}"
            };

            // Act
            var result = await agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.COMPLETED, result.Status);
            Assert.Contains("Top recommended candidate", result.ExecutionSummary);
            Assert.Contains(context.AuditTrail, a => a.ToolName == "GenerateRecommendationTool" && a.Action == "TOOL_COMPLETED");
            _mockEvaluationService.Verify(s => s.EvaluateAndRankCandidateVendorsAsync(It.IsAny<EvaluateVendorsRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_GetRecommendationIntent_ToolNotAllowListed_LogsSecurityViolationAndReturnsFailedStatus()
        {
            var requestId = Guid.NewGuid();
            var geminiJson = $@"{{ ""Action"": ""GET_RECOMMENDATION"", ""ProcurementRequestId"": ""{requestId}"" }}";

            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(geminiJson));

            // Only ScoreVendorsTool registered — GenerateRecommendationTool is NOT allow-listed
            var scoreTool = new ScoreVendorsTool(_mockEvaluationService.Object);
            var (agent, _) = CreateAgentWithTools(new IAgentTool[] { scoreTool });

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                RequesterRole = "MANAGER",
                Objective = $"Show me the recommendation for {requestId}"
            };

            var result = await agent.ExecuteAsync(context);

            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
            var securityAudit = Assert.Single(context.AuditTrail, a => a.Action == "TOOL_BLOCKED");
            Assert.True(securityAudit.IsSecurityViolation);
            Assert.Equal("GenerateRecommendationTool", securityAudit.ToolName);
        }

        [Fact]
        public async Task ExecuteAsync_GetRecommendationIntent_NoEvaluationExistsYet_ReturnsNeedsUserInputStatus()
        {
            var requestId = Guid.NewGuid();
            var geminiJson = $@"{{ ""Action"": ""GET_RECOMMENDATION"", ""ProcurementRequestId"": ""{requestId}"" }}";

            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(geminiJson));

            var recommendationTool = new GenerateRecommendationTool(_mockEvaluationService.Object);
            var (agent, _) = CreateAgentWithTools(new IAgentTool[] { recommendationTool });

            _mockEvaluationService.Setup(s => s.GetRecommendationSummaryAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementEvaluationSummaryDto?)null);

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                RequesterRole = "MANAGER",
                Objective = $"Show me the recommendation for {requestId}"
            };

            var result = await agent.ExecuteAsync(context);

            Assert.Equal(AgentExecutionStatus.NEEDS_USER_INPUT, result.Status);
            Assert.Contains("EvaluationResults", result.MissingFields);
            Assert.Contains("No evaluation has been run yet", result.ClarificationPrompt);
            Assert.Contains(context.AuditTrail, a => a.Action == "RECOMMENDATION_NOT_FOUND");
        }

        [Fact]
        public async Task ExecuteAsync_MissingProcurementRequestId_ReturnsNeedsUserInputStatus()
        {
            // Arrange: Gemini returns JSON with missing ProcurementRequestId
            var geminiJson = @"{
                ""HasSufficientInformation"": false,
                ""ClarificationPrompt"": ""Please provide a valid ProcurementRequestId.""
            }";

            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(geminiJson));

            var scoreTool = new ScoreVendorsTool(_mockEvaluationService.Object);
            var (agent, _) = CreateAgentWithTools(new IAgentTool[] { scoreTool });

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                RequesterRole = "PROCUREMENT_OFFICER",
                Objective = "Evaluate vendors"
            };

            // Act
            var result = await agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.NEEDS_USER_INPUT, result.Status);
            Assert.Contains("ProcurementRequestId", result.MissingFields);
            Assert.NotNull(result.ClarificationPrompt);
            Assert.Contains(context.AuditTrail, a => a.Action == "VALIDATION_FAILED");
        }

        [Fact]
        public async Task ExecuteAsync_NoQuotesInDatabase_ReturnsNeedsUserInputStatus()
        {
            // Arrange: Valid request ID, but service returns 0 candidates evaluated
            var requestId = Guid.NewGuid();
            var geminiJson = $@"{{ ""ProcurementRequestId"": ""{requestId}"" }}";

            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(geminiJson));

            var scoreTool = new ScoreVendorsTool(_mockEvaluationService.Object);
            var (agent, _) = CreateAgentWithTools(new IAgentTool[] { scoreTool });

            var emptySummary = new ProcurementEvaluationSummaryDto
            {
                ProcurementRequestId = requestId,
                TotalCandidatesEvaluated = 0,
                RecommendationSummary = "No vendor quotes found to evaluate."
            };

            _mockEvaluationService.Setup(s => s.EvaluateAndRankCandidateVendorsAsync(It.IsAny<EvaluateVendorsRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptySummary);

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                RequesterRole = "PROCUREMENT_OFFICER",
                Objective = $"Evaluate request {requestId}"
            };

            // Act
            var result = await agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.NEEDS_USER_INPUT, result.Status);
            Assert.Contains("VendorQuotes", result.MissingFields);
            Assert.Contains("No submitted vendor quotes found", result.ClarificationPrompt);
            Assert.Contains(context.AuditTrail, a => a.Action == "EVALUATION_PAUSED");
        }

        [Fact]
        public async Task ExecuteAsync_ValidationFailure_NegativeQuotedPrice_ReturnsFailedStatus()
        {
            // Arrange: Negative quoted price breaks deterministic validation rules
            var requestId = Guid.NewGuid();
            var geminiJson = $@"{{
                ""ProcurementRequestId"": ""{requestId}"",
                ""CandidateVendors"": [
                    {{
                        ""VendorId"": ""{Guid.NewGuid()}"",
                        ""QuotedPrice"": -100.0
                    }}
                ]
            }}";

            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(geminiJson));

            var scoreTool = new ScoreVendorsTool(_mockEvaluationService.Object);
            var (agent, _) = CreateAgentWithTools(new IAgentTool[] { scoreTool });

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                RequesterRole = "PROCUREMENT_OFFICER",
                Objective = "Evaluate vendors"
            };

            // Act
            var result = await agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
            Assert.Contains(result.ValidationErrors, e => e.Contains("QuotedPrice must be non-negative"));
            Assert.Contains(context.AuditTrail, a => a.Action == "VALIDATION_FAILED");
        }

        [Fact]
        public async Task ExecuteAsync_DisallowedToolRequested_LogsSecurityViolationAndReturnsFailedStatus()
        {
            // Arrange: ScoreVendorsTool is NOT registered in ToolRegistry
            var requestId = Guid.NewGuid();
            var geminiJson = $@"{{ ""ProcurementRequestId"": ""{requestId}"" }}";

            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(geminiJson));

            // Register NO tools to simulate ScoreVendorsTool not allowed
            var (agent, _) = CreateAgentWithTools(Enumerable.Empty<IAgentTool>());

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                RequesterRole = "PROCUREMENT_OFFICER",
                Objective = $"Evaluate request {requestId}"
            };

            // Act
            var result = await agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
            Assert.Contains("Security violation", result.ErrorMessages[0]);
            
            var securityAudit = Assert.Single(context.AuditTrail, a => a.Action == "TOOL_BLOCKED");
            Assert.True(securityAudit.IsSecurityViolation);
            Assert.Equal("ScoreVendorsTool", securityAudit.ToolName);
        }

        [Fact]
        public async Task ExecuteAsync_GeminiCallFails_ReturnsFailedStatus()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var errorMessage = "Gemini API rate limit or quota exceeded. Please try again later.";

            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Fail(errorMessage, isQuota: true));

            var scoreTool = new ScoreVendorsTool(_mockEvaluationService.Object);
            var (agent, _) = CreateAgentWithTools(new IAgentTool[] { scoreTool });

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                RequesterRole = "PROCUREMENT_OFFICER",
                ProcurementRequestId = requestId,
                Objective = $"Evaluate request {requestId}"
            };

            // Act
            var result = await agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
            Assert.Contains(errorMessage, result.ErrorMessages);
            Assert.Contains(context.AuditTrail, a => a.Action == "AGENT_FAILED" && a.Status == "FAILED");
            _mockEvaluationService.Verify(s => s.EvaluateAndRankCandidateVendorsAsync(It.IsAny<EvaluateVendorsRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_GeminiReturnsMalformedJson_ReturnsFailedStatus()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var malformedJson = "{ Action: SCORE_VENDORS, invalid json here ... ";

            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(malformedJson));

            var scoreTool = new ScoreVendorsTool(_mockEvaluationService.Object);
            var (agent, _) = CreateAgentWithTools(new IAgentTool[] { scoreTool });

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                RequesterRole = "PROCUREMENT_OFFICER",
                ProcurementRequestId = requestId,
                Objective = $"Evaluate request {requestId}"
            };

            // Act
            var result = await agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
            Assert.Contains("LLM returned malformed structured data.", result.ErrorMessages);
            Assert.Contains(context.AuditTrail, a => a.Action == "AGENT_FAILED" && a.Status == "FAILED");
            _mockEvaluationService.Verify(s => s.EvaluateAndRankCandidateVendorsAsync(It.IsAny<EvaluateVendorsRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
