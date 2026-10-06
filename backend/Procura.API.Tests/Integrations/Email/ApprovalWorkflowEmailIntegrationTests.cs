using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ApprovalWorkflowEntity = Procura.API.Modules.ApprovalWorkflow.Entities.ApprovalWorkflow;
using ProcurementRequestEntity = Procura.API.Modules.ProcurementRequest.Entities.ProcurementRequest;
using Procura.API.Modules.ApprovalWorkflow.Entities;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Modules.ApprovalWorkflow.Repositories;
using Procura.API.Modules.ApprovalWorkflow.Services;
using Procura.API.Modules.ProcurementRequest.Enums;
using Procura.API.Modules.ProcurementRequest.Repositories;
using Procura.API.Modules.VendorEvaluation.Services;
using Procura.API.Integrations.Email;
using Procura.API.Shared.Entities;
using Xunit;

namespace Procura.API.Tests.Integrations.Email
{
    public class ApprovalWorkflowEmailIntegrationTests
    {
        private readonly Mock<IApprovalWorkflowRepository> _workflowRepoMock;
        private readonly Mock<IProcurementRequestRepository> _requestRepoMock;
        private readonly WorkflowStateTransitionEngine _transitionEngine;
        private readonly Mock<IVendorEvaluationService> _vendorEvaluationServiceMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<ILogger<ApprovalWorkflowService>> _loggerMock;
        private readonly ApprovalWorkflowService _service;

        public ApprovalWorkflowEmailIntegrationTests()
        {
            _workflowRepoMock = new Mock<IApprovalWorkflowRepository>();
            _requestRepoMock = new Mock<IProcurementRequestRepository>();
            _transitionEngine = new WorkflowStateTransitionEngine();
            _vendorEvaluationServiceMock = new Mock<IVendorEvaluationService>();
            _emailServiceMock = new Mock<IEmailService>();
            _loggerMock = new Mock<ILogger<ApprovalWorkflowService>>();

            _service = new ApprovalWorkflowService(
                _workflowRepoMock.Object,
                _requestRepoMock.Object,
                _transitionEngine,
                _loggerMock.Object,
                _vendorEvaluationServiceMock.Object,
                _emailServiceMock.Object);
        }

        private static (ApprovalWorkflowEntity workflow, ProcurementRequestEntity request, Guid requesterId) CreateWorkflow(
            WorkflowState state = WorkflowState.WAITING_MANAGER_APPROVAL)
        {
            var requesterId = Guid.NewGuid();
            var requestId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequestNumber = "PR-1024",
                Title = "Office Laptop Procurement",
                RequesterId = requesterId,
                Requester = new User
                {
                    Id = requesterId,
                    FirstName = "Alice",
                    LastName = "Requester",
                    Email = "alice.requester@company.com"
                },
                Status = RequestStatus.PENDING_APPROVAL,
                EstimatedTotal = 5000m
            };

            var workflow = new ApprovalWorkflowEntity
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = requestId,
                ProcurementRequest = request,
                CurrentStatus = state,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return (workflow, request, requesterId);
        }

        [Fact]
        public async Task ApproveAsync_TriggersApprovalEmail_WithCorrectDetailsAndNoRejectionReason()
        {
            var (workflow, _, _) = CreateWorkflow();
            var managerId = Guid.NewGuid();
            var manager = new User { Id = managerId, FirstName = "John", LastName = "Manager", Email = "john@company.com" };

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id)).ReturnsAsync(workflow);
            _workflowRepoMock.Setup(r => r.GetUserByIdAsync(managerId)).ReturnsAsync(manager);

            var result = await _service.ApproveAsync(workflow.Id, managerId, "MANAGER", "Budget verified and approved.");

            Assert.Equal("APPROVED", result.CurrentStatus);

            _emailServiceMock.Verify(e => e.SendProcurementDecisionEmailAsync(
                "alice.requester@company.com",
                "Alice Requester",
                "PR-1024",
                "Office Laptop Procurement",
                "APPROVED",
                null,
                "John Manager"), Times.Once);
        }

        [Fact]
        public async Task RejectAsync_TriggersRejectionEmail_WithRejectionReasonAndCorrectDetails()
        {
            var (workflow, _, _) = CreateWorkflow();
            var managerId = Guid.NewGuid();
            var manager = new User { Id = managerId, FirstName = "John", LastName = "Manager", Email = "john@company.com" };

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id)).ReturnsAsync(workflow);
            _workflowRepoMock.Setup(r => r.GetUserByIdAsync(managerId)).ReturnsAsync(manager);

            var result = await _service.RejectAsync(workflow.Id, managerId, "MANAGER", "Budget exceeded for Q4.");

            Assert.Equal("REJECTED", result.CurrentStatus);

            _emailServiceMock.Verify(e => e.SendProcurementDecisionEmailAsync(
                "alice.requester@company.com",
                "Alice Requester",
                "PR-1024",
                "Office Laptop Procurement",
                "REJECTED",
                "Budget exceeded for Q4.",
                "John Manager"), Times.Once);
        }

        [Fact]
        public async Task ApproveAsync_WhenEmailServiceThrows_ApprovalStillSucceedsAndDoesNotFailTransaction()
        {
            var (workflow, _, _) = CreateWorkflow();
            var managerId = Guid.NewGuid();
            var manager = new User { Id = managerId, FirstName = "John", LastName = "Manager", Email = "john@company.com" };

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id)).ReturnsAsync(workflow);
            _workflowRepoMock.Setup(r => r.GetUserByIdAsync(managerId)).ReturnsAsync(manager);

            _emailServiceMock
                .Setup(e => e.SendProcurementDecisionEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()))
                .ThrowsAsync(new HttpRequestException("Resend API 500 Internal Server Error"));

            // Must NOT throw HttpRequestException
            var result = await _service.ApproveAsync(workflow.Id, managerId, "MANAGER", "Approved despite email outage.");

            Assert.Equal("APPROVED", result.CurrentStatus);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RejectAsync_WhenEmailServiceThrows_RejectionStillSucceedsAndDoesNotFailTransaction()
        {
            var (workflow, _, _) = CreateWorkflow();
            var managerId = Guid.NewGuid();
            var manager = new User { Id = managerId, FirstName = "John", LastName = "Manager", Email = "john@company.com" };

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id)).ReturnsAsync(workflow);
            _workflowRepoMock.Setup(r => r.GetUserByIdAsync(managerId)).ReturnsAsync(manager);

            _emailServiceMock
                .Setup(e => e.SendProcurementDecisionEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()))
                .ThrowsAsync(new HttpRequestException("Resend API unreachable"));

            var result = await _service.RejectAsync(workflow.Id, managerId, "MANAGER", "Rejected despite email outage.");

            Assert.Equal("REJECTED", result.CurrentStatus);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task IntermediateStateTransitions_DoNotTriggerDecisionEmails()
        {
            var (workflow, _, _) = CreateWorkflow(WorkflowState.AI_RECOMMENDATION_GENERATED);
            var officerId = Guid.NewGuid();

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id)).ReturnsAsync(workflow);

            await _service.TransitionWorkflowAsync(workflow.Id, WorkflowState.WAITING_MANAGER_APPROVAL, officerId, "PROCUREMENT_OFFICER");

            _emailServiceMock.Verify(e => e.SendProcurementDecisionEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task RequestRevisionAsync_DoesNotTriggerDecisionEmails()
        {
            var (workflow, _, _) = CreateWorkflow(WorkflowState.WAITING_MANAGER_APPROVAL);
            var managerId = Guid.NewGuid();

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id)).ReturnsAsync(workflow);

            await _service.RequestRevisionAsync(workflow.Id, managerId, "MANAGER", "Please reduce quantity.");

            _emailServiceMock.Verify(e => e.SendProcurementDecisionEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Never);
        }
    }
}
