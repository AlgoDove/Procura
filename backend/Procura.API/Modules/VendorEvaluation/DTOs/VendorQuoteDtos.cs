namespace Procura.API.Modules.VendorEvaluation.DTOs;

public record CreateVendorQuoteDto(
    Guid ProcurementRequestId,
    Guid VendorId,
    string VendorName,
    decimal QuotedPrice,
    int EstimatedDeliveryDays,
    decimal ReliabilityRating,
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