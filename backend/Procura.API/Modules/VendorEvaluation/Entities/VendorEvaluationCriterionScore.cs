using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Procura.API.Modules.VendorEvaluation.Enums;

namespace Procura.API.Modules.VendorEvaluation.Entities;

/// <summary>
/// Represents the score of a single vendor against a single evaluation criterion
/// within one VendorEvaluation, including the weight applied at evaluation time.
/// </summary>
public class VendorEvaluationCriterionScore
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid VendorEvaluationId { get; set; }

    [Required]
    public EvaluationCriterionType CriterionName { get; set; }

    [Required]
    [Column(TypeName = "decimal(5,2)")]
    public decimal Score { get; set; }

    [Required]
    [Column(TypeName = "decimal(4,3)")]
    public decimal Weight { get; set; }

    // Navigation property
    [ForeignKey(nameof(VendorEvaluationId))]
    public VendorEvaluation? VendorEvaluation { get; set; }
}
