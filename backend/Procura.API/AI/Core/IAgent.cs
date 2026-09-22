using System;
using System.Collections.Generic;

namespace Procura.API.AI.Core
{
    public class AgentResult
    {
        public AgentExecutionStatus Status { get; set; } = AgentExecutionStatus.COMPLETED;
        public string ExecutionSummary { get; set; } = string.Empty;
        public string? ClarificationPrompt { get; set; }
        public Guid? ProcurementRequestId { get; set; }
        public string? RequestNumber { get; set; }
        public decimal? EstimatedTotal { get; set; }
        public List<string> MissingFields { get; set; } = new();
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ErrorMessages { get; set; } = new();
        public object? OutputData { get; set; }
    }

    public interface IAgent
    {
        string AgentName { get; }
        WorkflowStage Stage { get; }
        Task<AgentResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default);
    }
}
