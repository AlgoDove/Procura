using System;
using System.Collections.Generic;
using System.Threading;
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
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Services;
using Procura.API.Shared.Entities;
using Xunit;

namespace Procura.API.Tests.Modules.ApprovalWorkflow.Services
{
    public class ApprovalWorkflowServiceTests
    {
        private readonly Mock<IApprovalWorkflowRepository> _workflowRepoMock;
        private readonly Mock<IProcurementRequestRepository> _requestRepoMock;
        private readonly WorkflowStateTransitionEngine _transitionEngine;
        private readonly Mock<IVendorEvaluationService> _vendorEvaluationServiceMock;
        private readonly Mock<ILogger<ApprovalWorkflowService>> _loggerMock;
        private readonly ApprovalWorkflowService _service;

        public ApprovalWorkflowServiceTests()
        {
            _workflowRepoMock = new Mock<IApprovalWorkflowRepository>();
            _requestRepoMock = new Mock<IProcurementRequestRepository>();
            _transitionEngine = new WorkflowStateTransitionEngine();
            _vendorEvaluationServiceMock = new Mock<IVendorEvaluationService>();
            _loggerMock = new Mock<ILogger<ApprovalWorkflowService>>();

            _service = new ApprovalWorkflowService(
                _workflowRepoMock.Object,
                _requestRepoMock.Object,
                _transitionEngine,
                _loggerMock.Object,
                _vendorEvaluationServiceMock.Object);
        }

        private static (ApprovalWorkflowEntity workflow, ProcurementRequestEntity request, Guid requesterId) CreateTestWorkflow(
            WorkflowState state = WorkflowState.WAITING_MANAGER_APPROVAL)
        {
            var requesterId = Guid.NewGuid();
            var requestId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequestNumber = "PR-2026-00001",
                Title = "High-Performance Workstations",
                RequesterId = requesterId,
                Requester = new User
                {
                    Id = requesterId,
                    FirstName = "Alice",
                    LastName = "Smith",
                    Email = "alice@procura.local"
                },
                Status = RequestStatus.PENDING_APPROVAL,
                EstimatedTotal = 50000m
            };

            var workflow = new ApprovalWorkflowEntity
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = requestId,
                ProcurementRequest = request,
                CurrentStatus = state,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddHours(-1)
            };

            request.ApprovalWorkflow = workflow;
            return (workflow, request, requesterId);
        }

        [Fact]
        public async Task ApproveAsync_AsManager_InWaitingApproval_Succeeds()
        {
            // Arrange
            var (workflow, request, _) = CreateTestWorkflow(WorkflowState.WAITING_MANAGER_APPROVAL);
            var managerId = Guid.NewGuid();

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            // Act
            var result = await _service.ApproveAsync(workflow.Id, managerId, "MANAGER", "Approved budget matches Q3 forecast.");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("APPROVED", result.CurrentStatus);
            Assert.NotNull(workflow.CompletedAt);
            Assert.Equal(RequestStatus.APPROVED, request.Status);
            Assert.Single(workflow.Decisions);

            var decision = workflow.Decisions.First();
            Assert.Equal(managerId, decision.ManagerId);
            Assert.Equal(ApprovalDecisionType.APPROVED, decision.Decision);
            Assert.Equal("Approved budget matches Q3 forecast.", decision.Comments);

            Assert.Contains(workflow.Notifications, n => n.Type == NotificationType.APPROVED);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Theory]
        [InlineData("EMPLOYEE")]
        [InlineData("PROCUREMENT_OFFICER")]
        [InlineData("ADMIN")] // Administrator rule: Admin does not hold operational approval authority
        public async Task ApproveAsync_NonManagerRole_ThrowsUnauthorizedAccessException(string unauthorizedRole)
        {
            var (workflow, _, _) = CreateTestWorkflow(WorkflowState.WAITING_MANAGER_APPROVAL);
            var userId = Guid.NewGuid();

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.ApproveAsync(workflow.Id, userId, unauthorizedRole, "Trying to approve"));

            Assert.Contains("Only an authorized MANAGER", ex.Message);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task ApproveAsync_WhenNotInWaitingApproval_ThrowsInvalidOperationException()
        {
            // Workflow is in DRAFT, not WAITING_MANAGER_APPROVAL
            var (workflow, _, _) = CreateTestWorkflow(WorkflowState.DRAFT);
            var managerId = Guid.NewGuid();

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ApproveAsync(workflow.Id, managerId, "MANAGER", "Premature approval"));

            Assert.Contains("Invalid state transition", ex.Message);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task RejectAsync_AsManager_WithValidReason_Succeeds()
        {
            // Arrange
            var (workflow, request, _) = CreateTestWorkflow(WorkflowState.WAITING_MANAGER_APPROVAL);
            var managerId = Guid.NewGuid();
            var rejectionReason = "Budget exceeds allocated division ceiling.";

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            // Act
            var result = await _service.RejectAsync(workflow.Id, managerId, "MANAGER", rejectionReason);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("REJECTED", result.CurrentStatus);
            Assert.NotNull(workflow.CompletedAt);
            Assert.Equal(RequestStatus.REJECTED, request.Status);
            Assert.Single(workflow.Decisions);

            var decision = workflow.Decisions.First();
            Assert.Equal(managerId, decision.ManagerId);
            Assert.Equal(ApprovalDecisionType.REJECTED, decision.Decision);
            Assert.Equal(rejectionReason, decision.Comments);

            Assert.Contains(workflow.Notifications, n => n.Type == NotificationType.REJECTED);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RejectAsync_MissingComments_ThrowsArgumentException(string? emptyComment)
        {
            var (workflow, _, _) = CreateTestWorkflow(WorkflowState.WAITING_MANAGER_APPROVAL);
            var managerId = Guid.NewGuid();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.RejectAsync(workflow.Id, managerId, "MANAGER", emptyComment!));

            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task RequestRevisionAsync_AsManager_WithComments_Succeeds()
        {
            // Arrange
            var (workflow, request, _) = CreateTestWorkflow(WorkflowState.WAITING_MANAGER_APPROVAL);
            var managerId = Guid.NewGuid();
            var revisionNotes = "Please reduce quantity of 4K monitors from 10 to 5.";

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            // Act
            var result = await _service.RequestRevisionAsync(workflow.Id, managerId, "MANAGER", revisionNotes);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("REVISION_REQUESTED", result.CurrentStatus);
            Assert.Null(workflow.CompletedAt); // Revision is not a terminal state
            Assert.Equal(RequestStatus.REVISION_REQUESTED, request.Status);
            Assert.Single(workflow.Decisions);

            var decision = workflow.Decisions.First();
            Assert.Equal(managerId, decision.ManagerId);
            Assert.Equal(ApprovalDecisionType.REVISION_REQUESTED, decision.Decision);
            Assert.Equal(revisionNotes, decision.Comments);

            Assert.Contains(workflow.Notifications, n => n.Type == NotificationType.REVISION_REQUESTED);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task InitializeWorkflowAsync_NewRequest_Succeeds()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var requestId = Guid.NewGuid();
            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequestNumber = "PR-2026-00002",
                RequesterId = requesterId,
                Status = RequestStatus.DRAFT
            };

            _workflowRepoMock.Setup(r => r.GetByProcurementRequestIdAsync(requestId))
                .ReturnsAsync((ApprovalWorkflowEntity?)null);

            _requestRepoMock.Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act
            var result = await _service.InitializeWorkflowAsync(requestId, requesterId, "EMPLOYEE");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("SUBMITTED", result.CurrentStatus);
            Assert.Equal(RequestStatus.SUBMITTED, request.Status);

            _workflowRepoMock.Verify(r => r.AddAsync(It.IsAny<ApprovalWorkflowEntity>()), Times.Once);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task InitializeWorkflowAsync_AlreadyExists_ThrowsInvalidOperationException()
        {
            var (workflow, _, requesterId) = CreateTestWorkflow();

            _workflowRepoMock.Setup(r => r.GetByProcurementRequestIdAsync(workflow.ProcurementRequestId))
                .ReturnsAsync(workflow);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.InitializeWorkflowAsync(workflow.ProcurementRequestId, requesterId, "EMPLOYEE"));
        }

        [Fact]
        public async Task GetWorkflowByIdAsync_EmployeeViewingOthersWorkflow_ThrowsUnauthorizedAccessException()
        {
            var (workflow, _, _) = CreateTestWorkflow();
            var otherEmployeeId = Guid.NewGuid();

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.GetWorkflowByIdAsync(workflow.Id, otherEmployeeId, "EMPLOYEE"));
        }

        [Fact]
        public async Task GetWorkflowByIdAsync_ManagerViewing_IncludesVendorRecommendation()
        {
            // Arrange
            var (workflow, _, _) = CreateTestWorkflow();
            var managerId = Guid.NewGuid();

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            _vendorEvaluationServiceMock.Setup(v => v.GetRecommendationSummaryAsync(workflow.ProcurementRequestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementEvaluationSummaryDto
                {
                    ProcurementRequestId = workflow.ProcurementRequestId,
                    TotalCandidatesEvaluated = 3,
                    TopScore = 95.5m,
                    RecommendationSummary = "Vendor A ranked highest based on cost and compliance."
                });

            // Act
            var result = await _service.GetWorkflowByIdAsync(workflow.Id, managerId, "MANAGER");

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.VendorRecommendationSummary);
            Assert.Equal(95.5m, result.VendorRecommendationSummary.TopScore);
            Assert.Equal(3, result.VendorRecommendationSummary.TotalCandidatesEvaluated);
        }

        [Fact]
        public async Task GetAuditTrailAsync_ReturnsCompleteAuditTrail()
        {
            // Arrange
            var (workflow, request, _) = CreateTestWorkflow();
            var managerId = Guid.NewGuid();

            workflow.Decisions.Add(new ApprovalDecision
            {
                Id = Guid.NewGuid(),
                ApprovalWorkflowId = workflow.Id,
                ManagerId = managerId,
                Decision = ApprovalDecisionType.APPROVED,
                Comments = "Audit test comment",
                CreatedAt = DateTime.UtcNow
            });

            workflow.AIAgentExecutions.Add(new AIAgentExecution
            {
                Id = Guid.NewGuid(),
                ApprovalWorkflowId = workflow.Id,
                AgentName = "ProcurementDecisionSupportAgent",
                ExecutionOrder = 1,
                ExecutionStatus = "COMPLETED",
                InputSummary = "Procurement summary",
                OutputSummary = "Risk analysis complete"
            });

            workflow.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                ApprovalWorkflowId = workflow.Id,
                RecipientUserId = request.RequesterId,
                Title = "Approval Milestone",
                Message = "Workflow approved.",
                Type = NotificationType.APPROVED
            });

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            // Act
            var audit = await _service.GetAuditTrailAsync(workflow.Id, managerId, "MANAGER");

            // Assert
            Assert.NotNull(audit);
            Assert.Equal(workflow.Id, audit.WorkflowId);
            Assert.Single(audit.Decisions);
            Assert.Single(audit.AgentExecutions);
            Assert.Single(audit.Notifications);
            Assert.Equal("ProcurementDecisionSupportAgent", audit.AgentExecutions.First().AgentName);
            Assert.Equal("Approval Milestone", audit.Notifications.First().Title);
        }
    }
}
