using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procura.API.Modules.VendorEvaluation.Entities;

/// <summary>
/// Represents a single vendor's evaluation result against a single procurement request,
/// including its rank, overall score, reasoning, and risk flags.
/// </summary>
public class VendorEvaluation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ProcurementRequestId { get; set; }

    [Required]
    public Guid VendorId { get; set; }

    [Required]
    public int Rank { get; set; }

    [Required]
    [Column(TypeName = "decimal(5,2)")]
    public decimal OverallScore { get; set; }

    [Required]
    public string Reasoning { get; set; } = string.Empty;

    public List<string>? RiskFlags { get; set; } = new();

    [Required]
    public bool GeneratedByAgent { get; set; } = false;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property: One VendorEvaluation contains one or more VendorEvaluationCriterionScores
    public ICollection<VendorEvaluationCriterionScore> CriterionScores { get; set; } = new List<VendorEvaluationCriterionScore>();
}
