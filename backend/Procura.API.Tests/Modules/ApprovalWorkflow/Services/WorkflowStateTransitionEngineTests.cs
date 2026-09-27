using System;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Modules.ApprovalWorkflow.Services;
using Procura.API.Modules.ProcurementRequest.Enums;
using Xunit;

namespace Procura.API.Tests.Modules.ApprovalWorkflow.Services
{
    public class WorkflowStateTransitionEngineTests
    {
        private readonly WorkflowStateTransitionEngine _engine = new();

        [Theory]
        [InlineData(WorkflowState.DRAFT, WorkflowState.SUBMITTED, "EMPLOYEE")]
        [InlineData(WorkflowState.DRAFT, WorkflowState.SUBMITTED, "ADMIN")]
        [InlineData(WorkflowState.SUBMITTED, WorkflowState.UNDER_VENDOR_EVALUATION, "PROCUREMENT_OFFICER")]
        [InlineData(WorkflowState.UNDER_VENDOR_EVALUATION, WorkflowState.AI_RECOMMENDATION_GENERATED, "PROCUREMENT_OFFICER")]
        [InlineData(WorkflowState.AI_RECOMMENDATION_GENERATED, WorkflowState.WAITING_MANAGER_APPROVAL, "PROCUREMENT_OFFICER")]
        [InlineData(WorkflowState.WAITING_MANAGER_APPROVAL, WorkflowState.APPROVED, "MANAGER")]
        [InlineData(WorkflowState.WAITING_MANAGER_APPROVAL, WorkflowState.REJECTED, "MANAGER")]
        [InlineData(WorkflowState.WAITING_MANAGER_APPROVAL, WorkflowState.REVISION_REQUESTED, "MANAGER")]
        [InlineData(WorkflowState.REVISION_REQUESTED, WorkflowState.DRAFT, "EMPLOYEE")]
        [InlineData(WorkflowState.APPROVED, WorkflowState.COMPLETED, "PROCUREMENT_OFFICER")]
        public void CanTransition_ValidTransitionsAndRoles_ReturnsTrue(
            WorkflowState current, WorkflowState target, string role)
        {
            var result = _engine.CanTransition(current, target, role, out var failureReason);

            Assert.True(result);
            Assert.Null(failureReason);
        }

        [Fact]
        public void ValidateTransitionOrThrow_ManagerCanApprove_Succeeds()
        {
            // Should not throw
            _engine.ValidateTransitionOrThrow(
                WorkflowState.WAITING_MANAGER_APPROVAL,
                WorkflowState.APPROVED,
                "MANAGER");
        }

        [Theory]
        [InlineData("EMPLOYEE")]
        [InlineData("PROCUREMENT_OFFICER")]
        [InlineData("ADMIN")] // Strict Requirement: Admin does NOT have operational approval authority
        public void ValidateTransitionOrThrow_NonManagerApproving_ThrowsUnauthorizedAccessException(string role)
        {
            var ex = Assert.Throws<UnauthorizedAccessException>(() =>
                _engine.ValidateTransitionOrThrow(
                    WorkflowState.WAITING_MANAGER_APPROVAL,
                    WorkflowState.APPROVED,
                    role));

            Assert.Contains("Only an authorized MANAGER", ex.Message);
        }

        [Theory]
        [InlineData("EMPLOYEE")]
        [InlineData("PROCUREMENT_OFFICER")]
        [InlineData("ADMIN")]
        public void ValidateTransitionOrThrow_NonManagerRejecting_ThrowsUnauthorizedAccessException(string role)
        {
            var ex = Assert.Throws<UnauthorizedAccessException>(() =>
                _engine.ValidateTransitionOrThrow(
                    WorkflowState.WAITING_MANAGER_APPROVAL,
                    WorkflowState.REJECTED,
                    role));

            Assert.Contains("Only an authorized MANAGER", ex.Message);
        }

        [Fact]
        public void ValidateTransitionOrThrow_InvalidGraphTransition_ThrowsInvalidOperationException()
        {
            // Attempting to jump directly from DRAFT to APPROVED
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _engine.ValidateTransitionOrThrow(
                    WorkflowState.DRAFT,
                    WorkflowState.APPROVED,
                    "MANAGER"));

            Assert.Contains("Invalid state transition", ex.Message);
        }

        [Theory]
        [InlineData(WorkflowState.REJECTED)]
        [InlineData(WorkflowState.COMPLETED)]
        public void ValidateTransitionOrThrow_FromTerminalState_ThrowsInvalidOperationException(WorkflowState terminalState)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _engine.ValidateTransitionOrThrow(
                    terminalState,
                    WorkflowState.APPROVED,
                    "MANAGER"));

            Assert.Contains("Invalid state transition", ex.Message);
        }

        [Theory]
        [InlineData(WorkflowState.DRAFT, RequestStatus.DRAFT)]
        [InlineData(WorkflowState.SUBMITTED, RequestStatus.SUBMITTED)]
        [InlineData(WorkflowState.UNDER_VENDOR_EVALUATION, RequestStatus.UNDER_EVALUATION)]
        [InlineData(WorkflowState.AI_RECOMMENDATION_GENERATED, RequestStatus.UNDER_EVALUATION)]
        [InlineData(WorkflowState.WAITING_MANAGER_APPROVAL, RequestStatus.PENDING_APPROVAL)]
        [InlineData(WorkflowState.APPROVED, RequestStatus.APPROVED)]
        [InlineData(WorkflowState.REJECTED, RequestStatus.REJECTED)]
        [InlineData(WorkflowState.REVISION_REQUESTED, RequestStatus.REVISION_REQUESTED)]
        [InlineData(WorkflowState.COMPLETED, RequestStatus.COMPLETED)]
        public void MapToProcurementRequestStatus_MapsAccurately(
            WorkflowState workflowState, RequestStatus expectedRequestStatus)
        {
            var mapped = _engine.MapToProcurementRequestStatus(workflowState);
            Assert.Equal(expectedRequestStatus, mapped);
        }
    }
}
