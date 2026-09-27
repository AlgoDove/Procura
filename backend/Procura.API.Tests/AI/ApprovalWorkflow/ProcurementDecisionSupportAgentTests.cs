using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Procura.API.AI.Agents.ApprovalWorkflow;
using Procura.API.AI.Agents.ApprovalWorkflow.Tools;
using Procura.API.AI.Core;
using Procura.API.AI.Gemini;
using Procura.API.Modules.ApprovalWorkflow.Entities;
using Procura.API.Modules.ApprovalWorkflow.Repositories;
using Procura.API.Modules.ProcurementRequest.Entities;
using Procura.API.Modules.ProcurementRequest.Repositories;
using Procura.API.Modules.VendorEvaluation.Entities;
using VendorEvaluationEntity = Procura.API.Modules.VendorEvaluation.Entities.VendorEvaluation;
using Procura.API.Modules.VendorEvaluation.Repositories;
using Procura.API.Modules.VendorManagement.Entities;
using Procura.API.Modules.VendorManagement.Repositories;
using Procura.API.AI.Orchestration;
using Procura.API.AI.Persistence;
using Procura.API.AI.Entities;
using Procura.API.AI.Agents.ProcurementRequest;
using Procura.API.AI.Agents.VendorManagement;
using Procura.API.AI.Agents.VendorEvaluation;
using Xunit;

namespace Procura.API.Tests.AI.ApprovalWorkflow
{
    public class ProcurementDecisionSupportAgentTests
    {
        private readonly Mock<IGeminiClient> _mockGeminiClient;
        private readonly Mock<IProcurementRequestRepository> _mockRequestRepo;
        private readonly Mock<IVendorQuoteRepository> _mockQuoteRepo;
        private readonly Mock<IVendorEvaluationRepository> _mockEvalRepo;
        private readonly Mock<IVendorRepository> _mockVendorRepo;
        private readonly Mock<IApprovalWorkflowRepository> _mockWorkflowRepo;
        private readonly Mock<ILogger<ProcurementDecisionSupportAgent>> _mockLogger;
        private readonly ApprovalDecisionDeterministicValidator _validator;
        private readonly ToolRegistry _toolRegistry;
        private readonly ProcurementDecisionSupportAgent _agent;

        public ProcurementDecisionSupportAgentTests()
        {
            _mockGeminiClient = new Mock<IGeminiClient>();
            _mockRequestRepo = new Mock<IProcurementRequestRepository>();
            _mockQuoteRepo = new Mock<IVendorQuoteRepository>();
            _mockEvalRepo = new Mock<IVendorEvaluationRepository>();
            _mockVendorRepo = new Mock<IVendorRepository>();
            _mockWorkflowRepo = new Mock<IApprovalWorkflowRepository>();
            _mockLogger = new Mock<ILogger<ProcurementDecisionSupportAgent>>();
            _validator = new ApprovalDecisionDeterministicValidator();

            var tools = new List<IAgentTool>
            {
                new EvaluateApprovalReadinessTool(_mockRequestRepo.Object, _mockQuoteRepo.Object, _mockEvalRepo.Object, _mockVendorRepo.Object),
                new GenerateExecutiveBriefTool(),
                new RecordAiAgentExecutionTool(_mockWorkflowRepo.Object)
            };

            _toolRegistry = new ToolRegistry(tools);

            _agent = new ProcurementDecisionSupportAgent(
                _mockGeminiClient.Object,
                _toolRegistry,
                _validator,
                _mockWorkflowRepo.Object,
                _mockLogger.Object);
        }

        [Fact]
        public void DeterministicValidator_WithMissingRequest_FailsValidation()
        {
            var data = new ApprovalReadinessData
            {
                ProcurementRequestId = Guid.Empty
            };

            var (isValid, errors, warnings) = _validator.ValidateReadiness(data);
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("Procurement Request ID is required"));
        }

        [Fact]
        public void DeterministicValidator_WithNoLineItems_FailsValidation()
        {
            var data = new ApprovalReadinessData
            {
                ProcurementRequestId = Guid.NewGuid(),
                ItemCount = 0
            };

            var (isValid, errors, warnings) = _validator.ValidateReadiness(data);
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("at least one line item"));
        }

        [Fact]
        public void DeterministicValidator_WithNoEvaluations_FailsValidation()
        {
            var data = new ApprovalReadinessData
            {
                ProcurementRequestId = Guid.NewGuid(),
                ItemCount = 2,
                HasEvaluations = false,
                TotalCandidatesEvaluated = 0
            };

            var (isValid, errors, warnings) = _validator.ValidateReadiness(data);
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("Vendor evaluation has not been conducted"));
        }

        [Fact]
        public void DeterministicValidator_WithNonCompliantVendor_FailsValidation()
        {
            var data = new ApprovalReadinessData
            {
                ProcurementRequestId = Guid.NewGuid(),
                ItemCount = 2,
                HasEvaluations = true,
                TotalCandidatesEvaluated = 2,
                TopVendorId = Guid.NewGuid(),
                IsComplianceApproved = false
            };

            var (isValid, errors, warnings) = _validator.ValidateReadiness(data);
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("Compliance Guardrail"));
        }

        [Fact]
        public void DeterministicValidator_WithOverBudgetQuote_GeneratesPolicyWarning()
        {
            var data = new ApprovalReadinessData
            {
                ProcurementRequestId = Guid.NewGuid(),
                ItemCount = 2,
                HasEvaluations = true,
                TotalCandidatesEvaluated = 2,
                TopVendorId = Guid.NewGuid(),
                EstimatedTotal = 10000m,
                TopQuotedPrice = 12500m,
                IsComplianceApproved = true
            };

            var (isValid, errors, warnings) = _validator.ValidateReadiness(data);
            Assert.True(isValid);
            Assert.Empty(errors);
            Assert.NotEmpty(warnings);
            Assert.Contains(warnings, w => w.Contains("Budget Alert"));
        }

        [Fact]
        public void DeterministicValidator_ExtractedAdvice_ApprovingNonCompliant_FailsValidation()
        {
            var data = new ApprovalReadinessData
            {
                IsComplianceApproved = false
            };
            var advice = new ExtractedDecisionSupportInput
            {
                RecommendedAction = "APPROVE",
                ExecutiveSummary = "Looks fine."
            };

            var (isValid, errors) = _validator.ValidateExtractedAdvice(advice, data);
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("Safety Violation"));
        }

        [Fact]
        public async Task Agent_ExecuteAsync_WhenMissingRequestId_ReturnsNeedsUserInput()
        {
            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                ExistingRequestId = null,
                Objective = "Assess approval readiness"
            };

            var result = await _agent.ExecuteAsync(context);

            Assert.Equal(AgentExecutionStatus.NEEDS_USER_INPUT, result.Status);
            Assert.NotNull(result.ClarificationPrompt);
        }

        [Fact]
        public async Task Agent_ExecuteAsync_WhenPrerequisitesNotSatisfied_ReturnsNeedsUserInput()
        {
            var requestId = Guid.NewGuid();
            var pr = new ProcurementRequest
            {
                Id = requestId,
                RequestNumber = "PR-2026-001",
                Title = "Test Server Request",
                EstimatedTotal = 50000m,
                Items = new List<ProcurementRequestItem>() // 0 items
            };

            _mockRequestRepo.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(pr);
            _mockQuoteRepo.Setup(q => q.GetByProcurementRequestIdAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Procura.API.Modules.VendorEvaluation.Entities.VendorQuote>());
            _mockEvalRepo.Setup(e => e.GetByProcurementRequestIdAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<VendorEvaluationEntity>)new List<VendorEvaluationEntity>());

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                ExistingRequestId = requestId,
                Objective = "Assess approval readiness"
            };

            var result = await _agent.ExecuteAsync(context);

            Assert.Equal(AgentExecutionStatus.NEEDS_USER_INPUT, result.Status);
            Assert.NotEmpty(result.ValidationErrors);
        }

        [Fact]
        public async Task Agent_ExecuteAsync_WithMockedGeminiSuccess_ParsesAndGeneratesDecisionBrief()
        {
            var requestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var pr = new ProcurementRequest
            {
                Id = requestId,
                RequestNumber = "PR-2026-0099",
                Title = "Network Switch Procurement",
                EstimatedTotal = 20000m,
                Items = new List<ProcurementRequestItem>
                {
                    new ProcurementRequestItem { Id = Guid.NewGuid(), ItemName = "Switch", Quantity = 2, EstimatedUnitPrice = 10000m }
                }
            };

            var quote = new Procura.API.Modules.VendorEvaluation.Entities.VendorQuote
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = requestId,
                VendorId = vendorId,
                VendorName = "SuperNet Inc",
                QuotedPrice = 18500m,
                EstimatedDeliveryDays = 7,
                IsComplianceApproved = true
            };

            var eval = new VendorEvaluationEntity
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = requestId,
                VendorId = vendorId,
                Rank = 1,
                OverallScore = 94.5m,
                Reasoning = "Excellent turnaround and pricing."
            };

            _mockRequestRepo.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(pr);
            _mockQuoteRepo.Setup(q => q.GetByProcurementRequestIdAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Procura.API.Modules.VendorEvaluation.Entities.VendorQuote> { quote });
            _mockEvalRepo.Setup(e => e.GetByProcurementRequestIdAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<VendorEvaluationEntity>)new List<VendorEvaluationEntity> { eval });
            _mockVendorRepo.Setup(v => v.GetByIdAsync(vendorId))
                .ReturnsAsync(new Vendor { Id = vendorId, Name = "SuperNet Inc" });

            var geminiJson = @"
{
  ""recommendedAction"": ""APPROVE"",
  ""confidenceScore"": 96.0,
  ""executiveSummary"": ""SuperNet Inc is highly recommended with a 94.5 overall score and $1,500 under-budget pricing."",
  ""budgetAssessment"": ""Price is 7.5% under budget ceiling."",
  ""riskFactors"": [],
  ""keyTradeoffs"": [""Fast delivery at competitive price""],
  ""conditionsOrStipulations"": [""Confirm warranty registration""]
}";

            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(geminiJson));

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                ExistingRequestId = requestId,
                Objective = "Formulate manager approval memo"
            };

            var result = await _agent.ExecuteAsync(context);

            Assert.Equal(AgentExecutionStatus.COMPLETED, result.Status);
            Assert.NotNull(result.OutputData);
            var brief = Assert.IsType<DecisionSupportOutput>(result.OutputData);
            Assert.Equal("APPROVE", brief.RecommendedAction);
            Assert.Equal(vendorId, brief.TopVendorId);
            Assert.Equal(18500m, brief.RecommendedAmount);
            Assert.True(brief.IsWithinBudget);
            Assert.True(brief.IsComplianceVerified);
            Assert.Equal(96.0m, brief.ConfidenceScore);
            Assert.Contains("SuperNet Inc is highly recommended", brief.ExecutiveBrief);

            // Verify AIAgentExecution was tracked
            _mockWorkflowRepo.Verify(w => w.AddAgentExecutionAsync(It.IsAny<AIAgentExecution>()), Times.Once);
            _mockWorkflowRepo.Verify(w => w.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Agent_ExecuteAsync_WithGeminiFailure_GracefullyUsesDeterministicFallback()
        {
            var requestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var pr = new ProcurementRequest
            {
                Id = requestId,
                RequestNumber = "PR-2026-0077",
                Title = "Workstation Upgrade",
                EstimatedTotal = 15000m,
                Items = new List<ProcurementRequestItem>
                {
                    new ProcurementRequestItem { Id = Guid.NewGuid(), ItemName = "PC", Quantity = 5, EstimatedUnitPrice = 3000m }
                }
            };

            var quote = new Procura.API.Modules.VendorEvaluation.Entities.VendorQuote
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = requestId,
                VendorId = vendorId,
                VendorName = "Workstation World",
                QuotedPrice = 14200m,
                EstimatedDeliveryDays = 10,
                IsComplianceApproved = true
            };

            var eval = new VendorEvaluationEntity
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = requestId,
                VendorId = vendorId,
                Rank = 1,
                OverallScore = 91.0m,
                Reasoning = "Solid price-to-performance ratio."
            };

            _mockRequestRepo.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(pr);
            _mockQuoteRepo.Setup(q => q.GetByProcurementRequestIdAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Procura.API.Modules.VendorEvaluation.Entities.VendorQuote> { quote });
            _mockEvalRepo.Setup(e => e.GetByProcurementRequestIdAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<VendorEvaluationEntity>)new List<VendorEvaluationEntity> { eval });
            _mockVendorRepo.Setup(v => v.GetByIdAsync(vendorId))
                .ReturnsAsync(new Vendor { Id = vendorId, Name = "Workstation World" });

            // Gemini fails (e.g. no key, network error)
            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Fail("Gemini API key is not configured.", isAuth: true));

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                ExistingRequestId = requestId,
                Objective = "Formulate manager approval memo"
            };

            var result = await _agent.ExecuteAsync(context);

            // Must succeed via graceful deterministic fallback!
            Assert.Equal(AgentExecutionStatus.COMPLETED, result.Status);
            var brief = Assert.IsType<DecisionSupportOutput>(result.OutputData);
            Assert.Equal("APPROVE", brief.RecommendedAction);
            Assert.Equal(vendorId, brief.TopVendorId);
            Assert.Equal(14200m, brief.RecommendedAmount);
            Assert.True(brief.IsWithinBudget);
            Assert.Contains("Workstation World", brief.ExecutiveBrief);
        }

        [Fact]
        public async Task Agent_ExecuteAsync_WhenSignificantlyOverBudget_FallbackRecommendsRevision()
        {
            var requestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var pr = new ProcurementRequest
            {
                Id = requestId,
                RequestNumber = "PR-2026-0088",
                Title = "Cloud Infrastructure License",
                EstimatedTotal = 10000m,
                Items = new List<ProcurementRequestItem>
                {
                    new ProcurementRequestItem { Id = Guid.NewGuid(), ItemName = "License", Quantity = 1, EstimatedUnitPrice = 10000m }
                }
            };

            var quote = new Procura.API.Modules.VendorEvaluation.Entities.VendorQuote
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = requestId,
                VendorId = vendorId,
                VendorName = "Expensive Cloud",
                QuotedPrice = 14500m, // 45% over budget
                EstimatedDeliveryDays = 3,
                IsComplianceApproved = true
            };

            var eval = new VendorEvaluationEntity
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = requestId,
                VendorId = vendorId,
                Rank = 1,
                OverallScore = 78.0m,
                Reasoning = "High turnaround score but high price."
            };

            _mockRequestRepo.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(pr);
            _mockQuoteRepo.Setup(q => q.GetByProcurementRequestIdAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Procura.API.Modules.VendorEvaluation.Entities.VendorQuote> { quote });
            _mockEvalRepo.Setup(e => e.GetByProcurementRequestIdAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<VendorEvaluationEntity>)new List<VendorEvaluationEntity> { eval });
            _mockVendorRepo.Setup(v => v.GetByIdAsync(vendorId))
                .ReturnsAsync(new Vendor { Id = vendorId, Name = "Expensive Cloud" });

            // Gemini offline
            _mockGeminiClient.Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Fail("Offline"));

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                ExistingRequestId = requestId,
                Objective = "Formulate approval memo"
            };

            var result = await _agent.ExecuteAsync(context);

            Assert.Equal(AgentExecutionStatus.COMPLETED, result.Status);
            var brief = Assert.IsType<DecisionSupportOutput>(result.OutputData);
            Assert.Equal("REQUEST_REVISION", brief.RecommendedAction);
            Assert.False(brief.IsWithinBudget);
            Assert.Contains("Revision requested", brief.ExecutiveBrief);
        }

        [Fact]
        public async Task CentralOrchestrator_WhenStageIsApprovalWorkflow_DelegatesToApprovalAgent_AndSetsWaitingForHumanApproval()
        {
            var workflowRepoMock = new Mock<IWorkflowRepository>();
            var procAgentMock = new Mock<IProcurementRequestAgent>();
            var vendorAgentMock = new Mock<IVendorManagementAgent>();
            var evalAgentMock = new Mock<IVendorEvaluationAgent>();
            var approvalAgentMock = new Mock<IProcurementDecisionSupportAgent>();
            var loggerMock = new Mock<ILogger<CentralOrchestrator>>();

            approvalAgentMock.Setup(a => a.AgentName).Returns("ProcurementDecisionSupportAgent");
            approvalAgentMock.Setup(a => a.Stage).Returns(WorkflowStage.APPROVAL_WORKFLOW);
            approvalAgentMock.Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentResult
                {
                    Status = AgentExecutionStatus.COMPLETED,
                    ExecutionSummary = "Approval memo ready."
                });

            var orchestrator = new CentralOrchestrator(
                workflowRepoMock.Object,
                procAgentMock.Object,
                vendorAgentMock.Object,
                evalAgentMock.Object,
                loggerMock.Object,
                approvalAgentMock.Object);

            var workflowId = Guid.NewGuid();
            var instance = new WorkflowInstance
            {
                Id = workflowId,
                RequesterId = Guid.NewGuid(),
                RequesterRole = "EMPLOYEE",
                CurrentStage = WorkflowStage.APPROVAL_WORKFLOW.ToString(),
                Status = WorkflowStatus.STAGE_COMPLETED.ToString(),
                ContextJson = ""
            };

            var plan = WorkflowPlan.CreateDefaultPlan();
            plan.Steps[0].Status = StepStatus.COMPLETED;
            plan.Steps[1].Status = StepStatus.COMPLETED;
            plan.Steps[2].Status = StepStatus.COMPLETED;

            var existingContext = new WorkflowContext
            {
                WorkflowId = workflowId,
                CurrentStage = WorkflowStage.APPROVAL_WORKFLOW,
                Status = WorkflowStatus.STAGE_COMPLETED,
                Plan = plan
            };

            instance.ContextJson = System.Text.Json.JsonSerializer.Serialize(existingContext);
            workflowRepoMock.Setup(r => r.GetByIdAsync(workflowId)).ReturnsAsync(instance);

            var resultContext = await orchestrator.ProcessWorkflowAsync(
                "Finalize approval",
                instance.RequesterId,
                instance.RequesterRole,
                null,
                workflowId);

            Assert.Equal(WorkflowStatus.WAITING_FOR_HUMAN_APPROVAL, resultContext.Status);
            Assert.Equal(WorkflowStage.APPROVAL_WORKFLOW, resultContext.CurrentStage);
            approvalAgentMock.Verify(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
