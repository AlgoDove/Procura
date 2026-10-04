namespace Procura.API.Modules.VendorEvaluation.DTOs;

/// <summary>
/// Detailed response DTO for a single vendor evaluation.
/// </summary>
public class VendorEvaluationResponseDto
{
    public Guid Id { get; set; }
    public Guid ProcurementRequestId { get; set; }
    public Guid VendorId { get; set; }
    public int Rank { get; set; }
    public decimal OverallScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string> RiskFlags { get; set; } = new();
    public bool GeneratedByAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CriterionScoreResponseDto> CriterionScores { get; set; } = new();
}
