using System;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Modules.ProcurementRequest.Enums;

namespace Procura.API.Modules.ApprovalWorkflow.Services
{
    /// <summary>
    /// Service contract defining workflow state transition rules, role-based transition validation,
    /// and synchronization with the parent ProcurementRequest status.
    /// </summary>
    public interface IWorkflowStateTransitionEngine
    {
        /// <summary>
        /// Evaluates whether a transition from current status to target status is valid for the given role.
        /// </summary>
        bool CanTransition(WorkflowState currentStatus, WorkflowState targetStatus, string userRole, out string? failureReason);

        /// <summary>
        /// Validates the transition and throws appropriate domain exceptions (InvalidOperationException / UnauthorizedAccessException) if invalid.
        /// </summary>
        void ValidateTransitionOrThrow(WorkflowState currentStatus, WorkflowState targetStatus, string userRole);

        /// <summary>
        /// Maps an ApprovalWorkflow state to the corresponding ProcurementRequest status for cross-module consistency.
        /// </summary>
        RequestStatus MapToProcurementRequestStatus(WorkflowState workflowState);
    }
}
