using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Procura.API.Modules.ApprovalWorkflow.DTOs;
using Procura.API.Modules.ApprovalWorkflow.Enums;

namespace Procura.API.Modules.ApprovalWorkflow.Services
{
    /// <summary>
    /// Service contract defining operations for managing approval workflow lifecycles,
    /// recording managerial decisions, and enforcing business rules.
    /// </summary>
    public interface IApprovalWorkflowService
    {
        /// <summary>
        /// Retrieves an ApprovalWorkflow by its ID with full audit decision history and vendor recommendation.
        /// Enforces user access isolation (EMPLOYEE can only view workflows of their own requests).
        /// </summary>
        Task<ApprovalWorkflowResponseDto?> GetWorkflowByIdAsync(Guid workflowId, Guid userId, string role);

        /// <summary>
        /// Retrieves the ApprovalWorkflow linked to a specific ProcurementRequest ID.
        /// </summary>
        Task<ApprovalWorkflowResponseDto?> GetWorkflowByProcurementRequestIdAsync(Guid procurementRequestId, Guid userId, string role);

        /// <summary>
        /// Retrieves all workflows currently awaiting managerial review (WAITING_MANAGER_APPROVAL).
        /// Requires MANAGER or ADMIN role.
        /// </summary>
        Task<IEnumerable<ApprovalWorkflowResponseDto>> GetPendingWorkflowsAsync(Guid userId, string role);

        /// <summary>
        /// Retrieves all workflows, filtered by user authorization and optional status.
        /// </summary>
        Task<IEnumerable<ApprovalWorkflowResponseDto>> GetAllWorkflowsAsync(Guid userId, string role, WorkflowState? status = null);

        /// <summary>
        /// Initializes a new ApprovalWorkflow for a procurement request (typically upon submission).
        /// </summary>
        Task<ApprovalWorkflowResponseDto> InitializeWorkflowAsync(Guid procurementRequestId, Guid userId, string role);

        /// <summary>
        /// Advances the workflow to a new valid state, validating business transition rules.
        /// </summary>
        Task<ApprovalWorkflowResponseDto> TransitionWorkflowAsync(Guid workflowId, WorkflowState targetStatus, Guid userId, string role);

        /// <summary>
        /// Records an APPROVED decision by an authorized Manager, finalizing approval.
        /// </summary>
        Task<ApprovalWorkflowResponseDto> ApproveAsync(Guid workflowId, Guid managerId, string role, string? comments);

        /// <summary>
        /// Records a REJECTED decision by an authorized Manager with required justification.
        /// </summary>
        Task<ApprovalWorkflowResponseDto> RejectAsync(Guid workflowId, Guid managerId, string role, string comments);

        /// <summary>
        /// Records a REVISION_REQUESTED decision by an authorized Manager with required revision comments.
        /// </summary>
        Task<ApprovalWorkflowResponseDto> RequestRevisionAsync(Guid workflowId, Guid managerId, string role, string comments);

        /// <summary>
        /// Retrieves the comprehensive audit trail for a workflow, including manager decisions,
        /// agent executions, and notification events.
        /// </summary>
        Task<WorkflowAuditTrailDto?> GetAuditTrailAsync(Guid workflowId, Guid userId, string role);
    }
}
