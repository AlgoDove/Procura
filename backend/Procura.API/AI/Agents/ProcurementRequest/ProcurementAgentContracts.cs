using System;
using System.Collections.Generic;
using Procura.API.Modules.ProcurementRequest.Enums;

namespace Procura.API.AI.Agents.ProcurementRequest
{
    public class ExtractedProcurementItemData
    {
        public string ItemName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Unit { get; set; } = "Piece";
        public decimal EstimatedUnitPrice { get; set; }
    }

    public class ExtractedProcurementData
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
        public Priority Priority { get; set; } = Priority.MEDIUM;
        public DateTime? RequiredByDate { get; set; }
        public List<ExtractedProcurementItemData> Items { get; set; } = new();

        public bool HasSufficientInformation { get; set; } = true;
        public List<string> MissingInformationReasons { get; set; } = new();
        public string? ClarificationPrompt { get; set; }
    }

    public class ProcurementValidationResult
    {
        public bool IsValid => Errors.Count == 0 && MissingFields.Count == 0;
        public List<string> Errors { get; set; } = new();
        public List<string> MissingFields { get; set; } = new();
    }

    public class ProcurementAgentInput
    {
        public Guid WorkflowId { get; set; }
        public string UserObjective { get; set; } = string.Empty;
        public Guid? ExistingRequestId { get; set; }
        public Guid RequesterId { get; set; }
        public string RequesterRole { get; set; } = "EMPLOYEE";
    }

    public class ProcurementAgentOutput
    {
        public bool Success { get; set; }
        public string Status { get; set; } = "COMPLETED"; // COMPLETED, NEEDS_USER_INPUT, FAILED
        public Guid? ProcurementRequestId { get; set; }
        public string? RequestNumber { get; set; }
        public decimal? EstimatedTotal { get; set; }
        public List<string> MissingFields { get; set; } = new();
        public List<string> ValidationErrors { get; set; } = new();
        public string SummaryMessage { get; set; } = string.Empty;
        public string? ClarificationPrompt { get; set; }
        public ExtractedProcurementData? ExtractedData { get; set; }
    }
}
