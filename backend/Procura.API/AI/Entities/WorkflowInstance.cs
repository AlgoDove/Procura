using System;

namespace Procura.API.AI.Entities
{
    public class WorkflowInstance
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Objective { get; set; } = string.Empty;
        public Guid RequesterId { get; set; }
        public string RequesterRole { get; set; } = "EMPLOYEE";
        public string CurrentStage { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public Guid? ProcurementRequestId { get; set; }
        public string? RequestNumber { get; set; }
        public decimal? EstimatedTotal { get; set; }

        public string? PlanJson { get; set; }
        public string? ContextJson { get; set; }
        public string? AuditTrailJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
