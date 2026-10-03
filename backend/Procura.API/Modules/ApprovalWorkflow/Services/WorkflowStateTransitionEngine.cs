using System;
using System.Collections.Generic;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Modules.ProcurementRequest.Enums;

namespace Procura.API.Modules.ApprovalWorkflow.Services
{
    /// <summary>
    /// Implements deterministic workflow state transition rules, role-based transition validation,
    /// and status synchronization between ApprovalWorkflow and ProcurementRequest.
    /// </summary>
    public class WorkflowStateTransitionEngine : IWorkflowStateTransitionEngine
    {
        // Set of legally allowable target states from any given state in the documented lifecycle
        private static readonly Dictionary<WorkflowState, HashSet<WorkflowState>> ValidStateGraph = new()
        {
            [WorkflowState.DRAFT] = new HashSet<WorkflowState>
            {
                WorkflowState.SUBMITTED
            },
            [WorkflowState.SUBMITTED] = new HashSet<WorkflowState>
            {
                WorkflowState.UNDER_VENDOR_EVALUATION
            },
            [WorkflowState.UNDER_VENDOR_EVALUATION] = new HashSet<WorkflowState>
            {
                WorkflowState.AI_RECOMMENDATION_GENERATED
            },
            [WorkflowState.AI_RECOMMENDATION_GENERATED] = new HashSet<WorkflowState>
            {
                WorkflowState.WAITING_MANAGER_APPROVAL
            },
            [WorkflowState.WAITING_MANAGER_APPROVAL] = new HashSet<WorkflowState>
            {
                WorkflowState.APPROVED,
                WorkflowState.REJECTED,
                WorkflowState.REVISION_REQUESTED
            },
            [WorkflowState.REVISION_REQUESTED] = new HashSet<WorkflowState>
            {
                WorkflowState.DRAFT,
                WorkflowState.SUBMITTED
            },
            [WorkflowState.APPROVED] = new HashSet<WorkflowState>
            {
                WorkflowState.COMPLETED
            },
            [WorkflowState.REJECTED] = new HashSet<WorkflowState>(), // Terminal
            [WorkflowState.COMPLETED] = new HashSet<WorkflowState>()  // Terminal
        };

        public bool CanTransition(WorkflowState currentStatus, WorkflowState targetStatus, string userRole, out string? failureReason)
        {
            failureReason = null;

            // 1. Check if current status is terminal
            if (currentStatus == WorkflowState.REJECTED || currentStatus == WorkflowState.COMPLETED)
            {
                failureReason = $"Cannot transition from terminal state {currentStatus}.";
                return false;
            }

            // 2. Check if the transition exists in the state graph
            if (!ValidStateGraph.TryGetValue(currentStatus, out var allowedTargets) || !allowedTargets.Contains(targetStatus))
            {
                failureReason = $"Invalid workflow state transition from {currentStatus} to {targetStatus}.";
                return false;
            }

            // 3. Enforce strict Role-Based Access Control on transitions
            var role = userRole.ToUpperInvariant();

            // Strict Rule: Manager actions (APPROVED, REJECTED, REVISION_REQUESTED)
            if (targetStatus == WorkflowState.APPROVED || targetStatus == WorkflowState.REJECTED || targetStatus == WorkflowState.REVISION_REQUESTED)
            {
                if (role != "MANAGER")
                {
                    failureReason = $"Role '{userRole}' is not authorized to finalize approval decisions. Only an authorized MANAGER holds approval authority.";
                    return false;
                }
                return true;
            }

            // Routing to Vendor Evaluation or AI Recommendation stages
            if (targetStatus == WorkflowState.UNDER_VENDOR_EVALUATION || 
                targetStatus == WorkflowState.AI_RECOMMENDATION_GENERATED ||
                targetStatus == WorkflowState.WAITING_MANAGER_APPROVAL)
            {
                if (role != "PROCUREMENT_OFFICER" && role != "ADMIN")
                {
                    failureReason = $"Role '{userRole}' is not authorized to transition workflow to {targetStatus}. Requires PROCUREMENT_OFFICER or ADMIN.";
                    return false;
                }
                return true;
            }

            // DRAFT -> SUBMITTED or REVISION_REQUESTED -> SUBMITTED
            if (targetStatus == WorkflowState.SUBMITTED)
            {
                if (role != "EMPLOYEE" && role != "ADMIN")
                {
                    failureReason = $"Role '{userRole}' is not authorized to submit procurement requests.";
                    return false;
                }
                return true;
            }

            // REVISION_REQUESTED -> DRAFT
            if (targetStatus == WorkflowState.DRAFT)
            {
                if (role != "EMPLOYEE" && role != "ADMIN")
                {
                    failureReason = $"Role '{userRole}' is not authorized to reset workflow to draft.";
                    return false;
                }
                return true;
            }

            // APPROVED -> COMPLETED
            if (targetStatus == WorkflowState.COMPLETED)
            {
                if (role != "PROCUREMENT_OFFICER" && role != "ADMIN")
                {
                    failureReason = $"Role '{userRole}' is not authorized to mark workflow as completed. Requires PROCUREMENT_OFFICER or ADMIN.";
                    return false;
                }
                return true;
            }

            failureReason = $"Unauthorized transition to {targetStatus} for role '{userRole}'.";
            return false;
        }

        public void ValidateTransitionOrThrow(WorkflowState currentStatus, WorkflowState targetStatus, string userRole)
        {
            // First check if transition is structurally permitted in the state machine
            if (!ValidStateGraph.TryGetValue(currentStatus, out var allowedTargets) || !allowedTargets.Contains(targetStatus))
            {
                throw new InvalidOperationException($"Invalid state transition: Cannot transition from {currentStatus} to {targetStatus}.");
            }

            // Then check role authorization
            if (!CanTransition(currentStatus, targetStatus, userRole, out var reason))
            {
                throw new UnauthorizedAccessException(reason ?? "User is not authorized to execute this state transition.");
            }
        }

        public RequestStatus MapToProcurementRequestStatus(WorkflowState workflowState)
        {
            return workflowState switch
            {
                WorkflowState.DRAFT => RequestStatus.DRAFT,
                WorkflowState.SUBMITTED => RequestStatus.SUBMITTED,
                WorkflowState.UNDER_VENDOR_EVALUATION => RequestStatus.UNDER_EVALUATION,
                WorkflowState.AI_RECOMMENDATION_GENERATED => RequestStatus.UNDER_EVALUATION,
                WorkflowState.WAITING_MANAGER_APPROVAL => RequestStatus.PENDING_APPROVAL,
                WorkflowState.APPROVED => RequestStatus.APPROVED,
                WorkflowState.REJECTED => RequestStatus.REJECTED,
                WorkflowState.REVISION_REQUESTED => RequestStatus.REVISION_REQUESTED,
                WorkflowState.COMPLETED => RequestStatus.COMPLETED,
                _ => throw new ArgumentOutOfRangeException(nameof(workflowState), $"Unsupported workflow state {workflowState}")
            };
        }
    }
}
