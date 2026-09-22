using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Entities;
using Procura.API.Modules.VendorEvaluation.Enums;

namespace Procura.API.Modules.VendorEvaluation.Services;

/// <summary>
/// Result of evaluating a single candidate vendor against criteria and procurement constraints.
/// </summary>
public class CandidateEvaluationResult
{
    public Guid VendorId { get; set; }
    public decimal OverallScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string> RiskFlags { get; set; } = new();
    public List<VendorEvaluationCriterionScore> CriterionScores { get; set; } = new();
}

/// <summary>
/// Scoring engine contract responsible for calculating individual criterion scores,
/// weighted overall scores, risk flags, and structured evaluation reasoning.
/// </summary>
public interface IVendorScoringEngine
{
    /// <summary>
    /// Evaluates a list of candidate vendors against procurement requirements and computes weighted scores, ranks, and risks.
    /// </summary>
    IReadOnlyList<CandidateEvaluationResult> EvaluateCandidates(
        Guid procurementRequestId,
        IReadOnlyList<CandidateVendorMetricDto> candidates,
        decimal? estimatedBudget,
        int? requiredDeliveryDays,
        IReadOnlyList<CriterionWeightConfigDto>? customWeights);

    /// <summary>
    /// Gets the standard weights for evaluation criteria.
    /// </summary>
    IReadOnlyDictionary<EvaluationCriterionType, decimal> GetDefaultWeights();
}
