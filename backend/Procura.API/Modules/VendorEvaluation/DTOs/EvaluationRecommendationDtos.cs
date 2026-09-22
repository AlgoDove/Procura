using System.ComponentModel.DataAnnotations;
using Procura.API.Modules.VendorEvaluation.Enums;

namespace Procura.API.Modules.VendorEvaluation.DTOs;

/// <summary>
/// DTO representing raw vendor metrics provided for automated evaluation.
/// </summary>
public class CandidateVendorMetricDto
{
    [Required]
    public Guid VendorId { get; set; }

    public string? VendorName { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Price quote must be non-negative.")]
    public decimal QuotedPrice { get; set; }

    [Range(0, 365, ErrorMessage = "Estimated delivery days must be realistic.")]
    public int EstimatedDeliveryDays { get; set; }

    [Range(0, 100, ErrorMessage = "Reliability rating must be between 0 and 100.")]
    public decimal ReliabilityRating { get; set; }

    public bool IsComplianceApproved { get; set; }

    public List<string>? KnownRisks { get; set; }
}

/// <summary>
/// Custom criterion weight specification for an evaluation run.
/// </summary>
public class CriterionWeightConfigDto
{
    public EvaluationCriterionType Criterion { get; set; }
    public decimal Weight { get; set; }
}

/// <summary>
/// Request to trigger evaluation & ranking on candidate vendors for a procurement request.
/// </summary>
public class EvaluateVendorsRequestDto
{
    [Required]
    public Guid ProcurementRequestId { get; set; }

    public decimal? EstimatedBudget { get; set; }

    public int? RequiredDeliveryDays { get; set; }

    public List<CriterionWeightConfigDto>? CustomWeights { get; set; }

    public List<CandidateVendorMetricDto> CandidateVendors { get; set; } = new();
}

/// <summary>
/// Summary recommendation response for human review and downstream Approval Workflow Management.
/// </summary>
public class ProcurementEvaluationSummaryDto
{
    public Guid ProcurementRequestId { get; set; }
    public int TotalCandidatesEvaluated { get; set; }
    public Guid? TopRecommendedVendorId { get; set; }
    public decimal? TopScore { get; set; }
    public string RecommendationSummary { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; }
    public List<VendorEvaluationResponseDto> RankedEvaluations { get; set; } = new();
}
