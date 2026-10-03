using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Modules.VendorEvaluation.DTOs;

namespace Procura.API.Modules.ApprovalWorkflow.DTOs
{
    /// <summary>
    /// Payload submitted when creating or initializing a workflow for a procurement request.
    /// </summary>
    public class CreateApprovalWorkflowDto
    {
        [Required(ErrorMessage = "ProcurementRequestId is required.")]
        public Guid ProcurementRequestId { get; set; }
    }

    /// <summary>
    /// Payload submitted by an authorized Manager when recording an approval decision.
    /// </summary>
    public class ApprovalDecisionRequestDto
    {
        /// <summary>
        /// Optional comment for approval; mandatory for rejection or revision request.
        /// </summary>
        [StringLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
        public string? Comments { get; set; }
    }

    /// <summary>
    /// Payload submitted when requesting a workflow state transition.
    /// </summary>
    public class TransitionWorkflowRequestDto
    {
        [Required(ErrorMessage = "TargetStatus is required.")]
        public WorkflowState TargetStatus { get; set; }
    }

    /// <summary>
    /// Detailed representation of an approval decision.
    /// </summary>
    public class ApprovalDecisionResponseDto
    {
        public Guid Id { get; set; }
        public Guid ManagerId { get; set; }
        public string ManagerName { get; set; } = string.Empty;
        public string Decision { get; set; } = string.Empty;
        public string? Comments { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Detailed representation of a workflow notification.
    /// </summary>
    public class NotificationResponseDto
    {
        public Guid Id { get; set; }
        public Guid RecipientUserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Full response model for an ApprovalWorkflow, including decision history,
    /// notifications, linked procurement request details, and vendor recommendation summary.
    /// </summary>
    public class ApprovalWorkflowResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProcurementRequestId { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public string RequestTitle { get; set; } = string.Empty;
        public decimal EstimatedTotal { get; set; }
        public Guid RequesterId { get; set; }
        public string RequesterName { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public List<ApprovalDecisionResponseDto> Decisions { get; set; } = new();
        public List<NotificationResponseDto> Notifications { get; set; } = new();

        /// <summary>
        /// Executive vendor evaluation summary produced by Component 3,
        /// presented to the authorized Manager for decision support.
        /// </summary>
        public ProcurementEvaluationSummaryDto? VendorRecommendationSummary { get; set; }
    }
}
