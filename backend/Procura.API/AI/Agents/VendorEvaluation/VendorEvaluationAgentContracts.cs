using System;
using System.Collections.Generic;
using Procura.API.Modules.VendorEvaluation.DTOs;

namespace Procura.API.AI.Agents.VendorEvaluation
{
    /// <summary>
    /// Structured payload extracted from natural language prompt by Gemini for Vendor Evaluation.
    /// </summary>
    public class ExtractedVendorEvaluationInput
    {
        public string Action { get; set; } = "SCORE_VENDORS"; // SCORE_VENDORS or GET_RECOMMENDATION
        public Guid? ProcurementRequestId { get; set; }
        public decimal? EstimatedBudget { get; set; }
        public int? RequiredDeliveryDays { get; set; }
        public List<CriterionWeightConfigDto>? CustomWeights { get; set; }
        public List<CandidateVendorMetricDto>? CandidateVendors { get; set; }
        public bool HasSufficientInformation { get; set; } = true;
        public List<string> MissingInformationReasons { get; set; } = new();
        public string? ClarificationPrompt { get; set; }
    }

    /// <summary>
    /// Deterministic validation output produced before tool execution or DB writes.
    /// </summary>
    public class VendorEvaluationValidationResult
    {
        public List<string> MissingFields { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public bool IsValid => MissingFields.Count == 0 && Errors.Count == 0;
    }
}

