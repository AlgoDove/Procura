using System.ComponentModel.DataAnnotations;
using Procura.API.Modules.VendorEvaluation.Enums;

namespace Procura.API.Modules.VendorEvaluation.DTOs;

/// <summary>
/// DTO for providing criterion score details when creating or updating an evaluation.
/// </summary>
public class CriterionScoreRequestDto
{
    [Required]
    public EvaluationCriterionType CriterionName { get; set; }

    [Required]
    [Range(0, 100, ErrorMessage = "Score must be between 0 and 100.")]
    public decimal Score { get; set; }

    [Required]
    [Range(0, 1, ErrorMessage = "Weight must be between 0 and 1.")]
    public decimal Weight { get; set; }
}

/// <summary>
/// DTO representing criterion score output in responses.
/// </summary>
public class CriterionScoreResponseDto
{
    public Guid Id { get; set; }
    public Guid VendorEvaluationId { get; set; }
    public EvaluationCriterionType CriterionName { get; set; }
    public decimal Score { get; set; }
    public decimal Weight { get; set; }
}
