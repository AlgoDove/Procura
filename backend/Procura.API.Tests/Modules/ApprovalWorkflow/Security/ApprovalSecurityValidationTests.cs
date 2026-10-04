using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ApprovalWorkflowEntity = Procura.API.Modules.ApprovalWorkflow.Entities.ApprovalWorkflow;
using ProcurementRequestEntity = Procura.API.Modules.ProcurementRequest.Entities.ProcurementRequest;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Modules.ApprovalWorkflow.Repositories;
using Procura.API.Modules.ApprovalWorkflow.Services;
using Procura.API.Modules.ProcurementRequest.Enums;
using Procura.API.Modules.ProcurementRequest.Repositories;
using Procura.API.Shared.Entities;
using Xunit;

namespace Procura.API.Tests.Modules.ApprovalWorkflow.Security
{
    /// <summary>
    /// Security & Validation Test Suite verifying multi-layer defense-in-depth:
    /// HTTP 400 Bad Request (Input bounds, Guid.Empty, MaxLength)
    /// HTTP 403 Forbidden (RBAC violation, Admin approval rejection, isolation)
    /// HTTP 404 Not Found (Nonexistent workflow/request)
    /// HTTP 409 Conflict (Illegal state transitions, terminal states)
    /// </summary>
    public class ApprovalSecurityValidationTests
    {
        private readonly Mock<IApprovalWorkflowRepository> _workflowRepoMock;
        private readonly Mock<IProcurementRequestRepository> _requestRepoMock;
        private readonly WorkflowStateTransitionEngine _transitionEngine;
        private readonly Mock<ILogger<ApprovalWorkflowService>> _loggerMock;
        private readonly ApprovalWorkflowService _service;

        public ApprovalSecurityValidationTests()
        {
            _workflowRepoMock = new Mock<IApprovalWorkflowRepository>();
            _requestRepoMock = new Mock<IProcurementRequestRepository>();
            _transitionEngine = new WorkflowStateTransitionEngine();
            _loggerMock = new Mock<ILogger<ApprovalWorkflowService>>();

            _service = new ApprovalWorkflowService(
                _workflowRepoMock.Object,
                _requestRepoMock.Object,
                _transitionEngine,
                _loggerMock.Object);
        }

        private static (ApprovalWorkflowEntity workflow, ProcurementRequestEntity request, Guid requesterId) CreateTestWorkflow(
            WorkflowState state = WorkflowState.WAITING_MANAGER_APPROVAL)
        {
            var requesterId = Guid.NewGuid();
            var requestId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequestNumber = "PR-2026-00099",
                Title = "Enterprise Server Rack",
                RequesterId = requesterId,
                Requester = new User { Id = requesterId, FirstName = "Bob", LastName = "Jones" },
                Status = RequestStatus.PENDING_APPROVAL,
                EstimatedTotal = 75000m
            };

            var workflow = new ApprovalWorkflowEntity
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = requestId,
                ProcurementRequest = request,
                CurrentStatus = state,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            };

            request.ApprovalWorkflow = workflow;
            return (workflow, request, requesterId);
        }

        [Fact]
        public async Task ApproveAsync_WithEmptyGuid_ThrowsArgumentException_400BadRequest()
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ApproveAsync(Guid.Empty, Guid.NewGuid(), "MANAGER", "Valid comment"));

            Assert.Contains("Workflow ID cannot be empty", ex.Message);
        }

        [Fact]
        public async Task ApproveAsync_WithExcessiveCommentLength_ThrowsArgumentException_400BadRequest()
        {
            var longComment = new string('A', 2001);
            var (workflow, _, _) = CreateTestWorkflow();

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ApproveAsync(workflow.Id, Guid.NewGuid(), "MANAGER", longComment));

            Assert.Contains("Comments cannot exceed 2000 characters", ex.Message);
        }

        [Fact]
        public async Task RejectAsync_WithExcessiveCommentLength_ThrowsArgumentException_400BadRequest()
        {
            var longComment = new string('B', 2001);
            var (workflow, _, _) = CreateTestWorkflow();

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.RejectAsync(workflow.Id, Guid.NewGuid(), "MANAGER", longComment));

            Assert.Contains("Comments cannot exceed 2000 characters", ex.Message);
        }

        [Fact]
        public async Task RequestRevisionAsync_WithExcessiveCommentLength_ThrowsArgumentException_400BadRequest()
        {
            var longComment = new string('C', 2001);
            var (workflow, _, _) = CreateTestWorkflow();

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.RequestRevisionAsync(workflow.Id, Guid.NewGuid(), "MANAGER", longComment));

            Assert.Contains("Comments cannot exceed 2000 characters", ex.Message);
        }

        [Theory]
        [InlineData("EMPLOYEE")]
        [InlineData("PROCUREMENT_OFFICER")]
        [InlineData("ADMIN")] // Strict System Rule: Administrator does not automatically receive approval authority
        public async Task ApproveAsync_UnauthorizedRoles_ThrowsUnauthorizedAccessException_403Forbidden(string role)
        {
            var (workflow, _, _) = CreateTestWorkflow();

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.ApproveAsync(workflow.Id, Guid.NewGuid(), role, "Unauthorized attempt"));

            Assert.Contains("Only an authorized MANAGER", ex.Message);
        }

        [Theory]
        [InlineData("EMPLOYEE")]
        [InlineData("PROCUREMENT_OFFICER")]
        [InlineData("ADMIN")]
        public async Task RejectAsync_UnauthorizedRoles_ThrowsUnauthorizedAccessException_403Forbidden(string role)
        {
            var (workflow, _, _) = CreateTestWorkflow();

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.RejectAsync(workflow.Id, Guid.NewGuid(), role, "Unauthorized reject"));

            Assert.Contains("Only an authorized MANAGER", ex.Message);
        }

        [Fact]
        public async Task ApproveAsync_NonexistentWorkflow_ThrowsKeyNotFoundException_404NotFound()
        {
            var nonexistentId = Guid.NewGuid();
            _workflowRepoMock.Setup(r => r.GetByIdAsync(nonexistentId))
                .ReturnsAsync((ApprovalWorkflowEntity?)null);

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _service.ApproveAsync(nonexistentId, Guid.NewGuid(), "MANAGER", "Approving ghost"));

            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public async Task ApproveAsync_OnAlreadyApprovedWorkflow_ThrowsInvalidOperationException_409Conflict()
        {
            var (workflow, _, _) = CreateTestWorkflow(WorkflowState.APPROVED);

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ApproveAsync(workflow.Id, Guid.NewGuid(), "MANAGER", "Double approve"));

            Assert.Contains("Invalid state transition", ex.Message);
        }

        [Fact]
        public async Task ApproveAsync_OnAlreadyRejectedWorkflow_ThrowsInvalidOperationException_409Conflict()
        {
            var (workflow, _, _) = CreateTestWorkflow(WorkflowState.REJECTED);

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ApproveAsync(workflow.Id, Guid.NewGuid(), "MANAGER", "Approve after reject"));

            Assert.Contains("Invalid state transition", ex.Message);
        }

        [Fact]
        public async Task GetWorkflowByIdAsync_EmployeeIsolatedToOwnRequests_ThrowsUnauthorizedAccessException_403Forbidden()
        {
            var (workflow, _, requesterId) = CreateTestWorkflow();
            var unauthorizedEmployeeId = Guid.NewGuid(); // Different employee

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflow.Id))
                .ReturnsAsync(workflow);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.GetWorkflowByIdAsync(workflow.Id, unauthorizedEmployeeId, "EMPLOYEE"));

            Assert.Contains("Not authorized to view another user's approval workflow", ex.Message);
        }
    }
}
