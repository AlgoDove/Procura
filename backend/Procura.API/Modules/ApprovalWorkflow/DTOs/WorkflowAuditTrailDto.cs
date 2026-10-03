using System;
using System.Collections.Generic;
using Procura.API.Modules.ApprovalWorkflow.DTOs;

namespace Procura.API.Modules.ApprovalWorkflow.DTOs
{
    /// <summary>
    /// Auditable execution record of an AI agent within the workflow.
    /// Does not store internal LLM chain-of-thought or reasoning steps.
    /// </summary>
    public class AIAgentExecutionResponseDto
    {
        public Guid Id { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public int ExecutionOrder { get; set; }
        public string ExecutionStatus { get; set; } = string.Empty;
        public string? InputSummary { get; set; }
        public string? OutputSummary { get; set; }
        public string? ValidationResult { get; set; }
        public string? ToolExecutionMetadata { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Comprehensive audit trail detailing workflow lifecycle transitions,
    /// manager decisions, AI executions, and dispatched notification events.
    /// </summary>
    public class WorkflowAuditTrailDto
    {
        public Guid WorkflowId { get; set; }
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

        /// <summary>
        /// Immutable managerial decision history.
        /// </summary>
        public List<ApprovalDecisionResponseDto> Decisions { get; set; } = new();

        /// <summary>
        /// Auditable history of AI agent executions.
        /// </summary>
        public List<AIAgentExecutionResponseDto> AgentExecutions { get; set; } = new();

        /// <summary>
        /// Complete event notifications generated for this workflow.
        /// </summary>
        public List<NotificationResponseDto> Notifications { get; set; } = new();
    }
}
