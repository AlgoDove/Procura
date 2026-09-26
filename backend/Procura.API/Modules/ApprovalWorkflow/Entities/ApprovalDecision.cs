using System;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Shared.Entities;

namespace Procura.API.Modules.ApprovalWorkflow.Entities
{
    /// <summary>
    /// Represents an immutable decision record submitted by an authorized Manager
    /// during the WAITING_MANAGER_APPROVAL stage.
    /// </summary>
    public class ApprovalDecision
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Foreign key to the parent ApprovalWorkflow.
        /// </summary>
        public Guid ApprovalWorkflowId { get; set; }

        /// <summary>
        /// Navigation property to the parent ApprovalWorkflow.
        /// </summary>
        public ApprovalWorkflow ApprovalWorkflow { get; set; } = null!;

        /// <summary>
        /// Foreign key identifying the Manager who made this decision.
        /// Integrates directly with the existing User entity.
        /// </summary>
        public Guid ManagerId { get; set; }

        /// <summary>
        /// Navigation property to the existing User entity (acting as Manager).
        /// </summary>
        public User Manager { get; set; } = null!;

        /// <summary>
        /// The formal decision: APPROVED, REJECTED, or REVISION_REQUESTED.
        /// </summary>
        public ApprovalDecisionType Decision { get; set; }

        /// <summary>
        /// Optional or required managerial remarks/justification for the decision.
        /// </summary>
        public string? Comments { get; set; }

        /// <summary>
        /// Timestamp when the decision was finalized and recorded.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
