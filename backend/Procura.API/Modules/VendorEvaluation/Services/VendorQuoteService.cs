using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Entities;
using Procura.API.Modules.VendorEvaluation.Repositories;
using Procura.API.Modules.VendorManagement.Enums;
using Procura.API.Modules.VendorManagement.Services;

namespace Procura.API.Modules.VendorEvaluation.Services;

public class VendorQuoteService : IVendorQuoteService
{
    private readonly IVendorQuoteRepository _repository;
    private readonly IVendorService _vendorService;

    public VendorQuoteService(IVendorQuoteRepository repository, IVendorService vendorService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _vendorService = vendorService ?? throw new ArgumentNullException(nameof(vendorService));
    }

    public async Task<VendorQuoteResponseDto> SubmitQuoteAsync(CreateVendorQuoteDto dto, CancellationToken cancellationToken = default)
    {
        var vendor = await _vendorService.GetByIdAsync(dto.VendorId);

        if (vendor == null)
        {
            throw new InvalidOperationException($"Vendor {dto.VendorId} does not exist in Vendor Management.");
        }

        if (vendor.Status == VendorStatus.INACTIVE)
        {
            throw new InvalidOperationException($"Vendor '{vendor.Name}' is deactivated and cannot be quoted.");
        }

        var quote = new VendorQuote
        {
            Id = Guid.NewGuid(),
            ProcurementRequestId = dto.ProcurementRequestId,
            VendorId = dto.VendorId,
            VendorName = dto.VendorName,
            QuotedPrice = dto.QuotedPrice,
            EstimatedDeliveryDays = dto.EstimatedDeliveryDays,
            ReliabilityRating = dto.ReliabilityRating,
            IsComplianceApproved = dto.IsComplianceApproved,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(quote, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return MapToDto(quote);
    }

    public async Task<IReadOnlyList<VendorQuoteResponseDto>> GetQuotesByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var quotes = await _repository.GetByProcurementRequestIdAsync(requestId, cancellationToken);
        return quotes.Select(MapToDto).ToList();
    }

    public async Task<VendorQuoteResponseDto?> GetQuoteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quote = await _repository.GetByIdAsync(id, cancellationToken);
        return quote == null ? null : MapToDto(quote);
    }

    private static VendorQuoteResponseDto MapToDto(VendorQuote q) => new(
        q.Id, q.ProcurementRequestId, q.VendorId, q.VendorName,
        q.QuotedPrice, q.EstimatedDeliveryDays, q.ReliabilityRating,
        q.IsComplianceApproved, q.Notes, q.CreatedAt
    );
}