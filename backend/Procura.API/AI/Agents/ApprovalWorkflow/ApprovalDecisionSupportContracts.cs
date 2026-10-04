using System;
using System.Collections.Generic;

namespace Procura.API.AI.Agents.ApprovalWorkflow
{
    /// <summary>
    /// Structured payload extracted from Gemini LLM for managerial decision support.
    /// </summary>
    public class ExtractedDecisionSupportInput
    {
        public string RecommendedAction { get; set; } = "APPROVE";
        public decimal ConfidenceScore { get; set; } = 90.0m;
        public string ExecutiveSummary { get; set; } = string.Empty;
        public string BudgetAssessment { get; set; } = string.Empty;
        public List<string> RiskFactors { get; set; } = new();
        public List<string> KeyTradeoffs { get; set; } = new();
        public List<string> ConditionsOrStipulations { get; set; } = new();
    }

    /// <summary>
    /// Final auditable decision support brief generated for human Manager review.
    /// </summary>
    public class DecisionSupportOutput
    {
        public Guid ProcurementRequestId { get; set; }
        public Guid WorkflowId { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public string RequestTitle { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = "APPROVE";
        public Guid? TopVendorId { get; set; }
        public string? TopVendorName { get; set; }
        public decimal RecommendedAmount { get; set; }
        public decimal BudgetCap { get; set; }
        public bool IsWithinBudget { get; set; }
        public bool IsComplianceVerified { get; set; }
        public decimal ConfidenceScore { get; set; }
        public string ExecutiveBrief { get; set; } = string.Empty;
        public List<string> RiskFactors { get; set; } = new();
        public List<string> KeyTradeoffs { get; set; } = new();
        public List<string> ConditionsOrStipulations { get; set; } = new();
        public bool GeneratedByAgent { get; set; } = true;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Context data gathered by EvaluateApprovalReadinessTool for validation and briefing.
    /// </summary>
    public class ApprovalReadinessData
    {
        public Guid ProcurementRequestId { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public decimal EstimatedTotal { get; set; }
        public int ItemCount { get; set; }
        public Guid? TopVendorId { get; set; }
        public string? TopVendorName { get; set; }
        public decimal? TopQuotedPrice { get; set; }
        public int? TopDeliveryDays { get; set; }
        public bool IsComplianceApproved { get; set; }
        public decimal? TopScore { get; set; }
        public string? Component3Summary { get; set; }
        public bool HasQuotes { get; set; }
        public bool HasEvaluations { get; set; }
        public int TotalCandidatesEvaluated { get; set; }
    }
}
