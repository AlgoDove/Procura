using System;

namespace Procura.API.AI.Core
{
    public class WorkflowAuditEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid WorkflowId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public WorkflowStage Stage { get; set; }
        public string Actor { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? ToolName { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public bool IsSecurityViolation { get; set; } = false;
    }
}
