using System.ComponentModel.DataAnnotations;

namespace Procura.API.Modules.VendorEvaluation.DTOs;

/// <summary>
/// DTO for creating a new vendor evaluation record.
/// </summary>
public class CreateVendorEvaluationDto
{
    [Required]
    public Guid ProcurementRequestId { get; set; }

    [Required]
    public Guid VendorId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Rank must be 1 or greater.")]
    public int Rank { get; set; }

    [Required]
    [Range(0, 100, ErrorMessage = "Overall score must be between 0 and 100.")]
    public decimal OverallScore { get; set; }

    [Required]
    public string Reasoning { get; set; } = string.Empty;

    public List<string>? RiskFlags { get; set; } = new();

    public bool GeneratedByAgent { get; set; } = false;

    [Required]
    [MinLength(1, ErrorMessage = "At least one criterion score is required.")]
    public List<CriterionScoreRequestDto> CriterionScores { get; set; } = new();
}

/// <summary>
/// DTO for updating an existing vendor evaluation record.
/// </summary>
public class UpdateVendorEvaluationDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Rank must be 1 or greater.")]
    public int? Rank { get; set; }

    [Range(0, 100, ErrorMessage = "Overall score must be between 0 and 100.")]
    public decimal? OverallScore { get; set; }

    public string? Reasoning { get; set; }

    public List<string>? RiskFlags { get; set; }

    public List<CriterionScoreRequestDto>? CriterionScores { get; set; }
}
