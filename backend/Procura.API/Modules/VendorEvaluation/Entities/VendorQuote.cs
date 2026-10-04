using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procura.API.Modules.VendorEvaluation.Entities;

/// <summary>
/// Represents a vendor's quote submitted for a specific procurement request.
/// </summary>
public class VendorQuote
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ProcurementRequestId { get; set; }

    [Required]
    public Guid VendorId { get; set; }

    [Required]
    [MaxLength(150)]
    public string VendorName { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal QuotedPrice { get; set; }

    [Required]
    public int EstimatedDeliveryDays { get; set; }

    [Required]
    [Column(TypeName = "decimal(5,2)")]
    public decimal ReliabilityRating { get; set; }

    [Required]
    public bool IsComplianceApproved { get; set; }

    [Column(TypeName = "text")]
    public string? Notes { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}