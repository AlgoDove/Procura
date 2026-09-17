using System;
using System.Collections.Generic;

namespace Procura.API.AI.Core
{
    public class WorkflowContext
    {
        public Guid WorkflowId { get; set; } = Guid.NewGuid();
        public string Objective { get; set; } = string.Empty;
        public Guid RequesterId { get; set; }
        public string RequesterRole { get; set; } = "EMPLOYEE";
        public Guid? ExistingRequestId { get; set; }

        public WorkflowStage CurrentStage { get; set; } = WorkflowStage.PROCUREMENT_REQUEST;
        public WorkflowStatus Status { get; set; } = WorkflowStatus.NOT_STARTED;
        public WorkflowPlan Plan { get; set; } = WorkflowPlan.CreateDefaultPlan();

        public Guid? ProcurementRequestId { get; set; }
        public string? RequestNumber { get; set; }
        public decimal? EstimatedTotal { get; set; }

        public string? ClarificationPrompt { get; set; }
        public string? ExecutionSummary { get; set; }

        public List<string> Errors { get; set; } = new();
        public List<WorkflowAuditEntry> AuditTrail { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public void AddAudit(WorkflowStage stage, string actor, string action, string status, string details, string? toolName = null, bool isSecurityViolation = false)
        {
            AuditTrail.Add(new WorkflowAuditEntry
            {
                WorkflowId = WorkflowId,
                Stage = stage,
                Actor = actor,
                Action = action,
                Status = status,
                Details = details,
                ToolName = toolName,
                IsSecurityViolation = isSecurityViolation,
                Timestamp = DateTime.UtcNow
            });
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
