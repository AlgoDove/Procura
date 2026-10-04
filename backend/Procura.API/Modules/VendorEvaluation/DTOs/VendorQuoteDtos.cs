using System.ComponentModel.DataAnnotations;

namespace Procura.API.Modules.VendorEvaluation.DTOs;

public record CreateVendorQuoteDto(
    [Required] Guid ProcurementRequestId,
    [Required] Guid VendorId,
    [Required] string VendorName,
    [Range(0, double.MaxValue, ErrorMessage = "QuotedPrice must be non-negative.")] decimal QuotedPrice,
    [Range(0, 365, ErrorMessage = "EstimatedDeliveryDays must be between 0 and 365.")] int EstimatedDeliveryDays,
    [Range(0, 100, ErrorMessage = "ReliabilityRating must be between 0 and 100 (or 0.0 to 5.0).")] decimal ReliabilityRating,
    bool IsComplianceApproved,
    string? Notes
);

public record VendorQuoteResponseDto(
    Guid Id,
    Guid ProcurementRequestId,
    Guid VendorId,
    string VendorName,
    decimal QuotedPrice,
    int EstimatedDeliveryDays,
    decimal ReliabilityRating,
    bool IsComplianceApproved,
    string? Notes,
    DateTime CreatedAt
);