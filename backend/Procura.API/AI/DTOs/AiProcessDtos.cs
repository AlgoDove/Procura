using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Procura.API.AI.Core;

namespace Procura.API.AI.DTOs
{
    public class ProcessProcurementAiRequestDto
    {
        [Required(ErrorMessage = "Procurement objective or description is required.")]
        public string Objective { get; set; } = string.Empty;

        public Guid? ExistingRequestId { get; set; }
        public Guid? WorkflowId { get; set; }
    }

    public class WorkflowProcessResponseDto
    {
        public Guid WorkflowId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CurrentStage { get; set; } = string.Empty;
        public Guid? ProcurementRequestId { get; set; }
        public string? RequestNumber { get; set; }
        public decimal? EstimatedTotal { get; set; }
        public string? ExecutionSummary { get; set; }
        public string? ClarificationPrompt { get; set; }
        public List<string> Errors { get; set; } = new();
        public WorkflowPlan? Plan { get; set; }
        public List<WorkflowAuditEntry> AuditTrail { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }
}
