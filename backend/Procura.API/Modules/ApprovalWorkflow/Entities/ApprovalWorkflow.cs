using System;
using System.Collections.Generic;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using ProcurementRequestEntity = Procura.API.Modules.ProcurementRequest.Entities.ProcurementRequest;

namespace Procura.API.Modules.ApprovalWorkflow.Entities
{
    /// <summary>
    /// Represents the approval workflow orchestrating the review, evaluation,
    /// and final managerial decision for a specific ProcurementRequest.
    /// </summary>
    public class ApprovalWorkflow
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Foreign key pointing to the associated ProcurementRequest.
        /// </summary>
        public Guid ProcurementRequestId { get; set; }

        /// <summary>
        /// Navigation property to the existing ProcurementRequest entity.
        /// </summary>
        public ProcurementRequestEntity ProcurementRequest { get; set; } = null!;

        /// <summary>
        /// Current lifecycle state of the workflow.
        /// </summary>
        public WorkflowState CurrentStatus { get; set; } = WorkflowState.DRAFT;

        /// <summary>
        /// Timestamp when the workflow was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the workflow state was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the workflow reached a terminal state (APPROVED, REJECTED, or COMPLETED).
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// History of managerial approval decisions recorded for this workflow.
        /// </summary>
        public ICollection<ApprovalDecision> Decisions { get; set; } = new List<ApprovalDecision>();

        /// <summary>
        /// Audit records of AI agent executions performed during this workflow.
        /// </summary>
        public ICollection<AIAgentExecution> AIAgentExecutions { get; set; } = new List<AIAgentExecution>();

        /// <summary>
        /// Notifications dispatched as a result of lifecycle transitions in this workflow.
        /// </summary>
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
