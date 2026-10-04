using Microsoft.EntityFrameworkCore;
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Entities;
using Procura.API.Modules.VendorEvaluation.Repositories;
using Procura.API.Modules.VendorManagement.DTOs;
using Procura.API.Modules.VendorManagement.Enums;
using Procura.API.Modules.VendorManagement.Services;
using Procura.API.Shared.Data;

namespace Procura.API.Modules.VendorEvaluation.Services;

public class VendorQuoteService : IVendorQuoteService
{
    private readonly IVendorQuoteRepository _repository;
    private readonly IVendorService _vendorService;
    private readonly ApplicationDbContext _dbContext;

    public VendorQuoteService(
        IVendorQuoteRepository repository,
        IVendorService vendorService,
        ApplicationDbContext dbContext)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _vendorService = vendorService ?? throw new ArgumentNullException(nameof(vendorService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
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

        // Enforce vendor selection: if selections exist for this request, verify the vendor is selected
        var hasSelections = await _dbContext.VendorSelections
            .AnyAsync(vs => vs.ProcurementRequestId == dto.ProcurementRequestId, cancellationToken);

        if (hasSelections)
        {
            var isSelected = await _dbContext.VendorSelections
                .AnyAsync(vs => vs.ProcurementRequestId == dto.ProcurementRequestId && vs.VendorId == dto.VendorId, cancellationToken);

            if (!isSelected)
            {
                throw new InvalidOperationException($"Vendor '{vendor.Name}' is not a selected vendor for Procurement Request {dto.ProcurementRequestId}.");
            }
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

    public async Task<IReadOnlyList<VendorResponseDto>> GetSelectedVendorsForRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var selectedVendorIds = await _dbContext.VendorSelections
            .Where(vs => vs.ProcurementRequestId == requestId)
            .Select(vs => vs.VendorId)
            .ToListAsync(cancellationToken);

        if (!selectedVendorIds.Any())
        {
            return Array.Empty<VendorResponseDto>();
        }

        var vendors = await _dbContext.Vendors
            .Where(v => selectedVendorIds.Contains(v.Id) && v.Status == VendorStatus.ACTIVE)
            .ToListAsync(cancellationToken);

        return vendors.Select(v => new VendorResponseDto
        {
            Id = v.Id,
            Name = v.Name,
            ContactPerson = v.ContactPerson,
            Email = v.Email,
            PhoneNumber = v.PhoneNumber,
            Address = v.Address,
            Category = v.Category,
            Rating = v.Rating,
            Status = v.Status,
            CreatedAt = v.CreatedAt,
            UpdatedAt = v.UpdatedAt
        }).ToList();
    }

    private static VendorQuoteResponseDto MapToDto(VendorQuote q) => new(
        q.Id, q.ProcurementRequestId, q.VendorId, q.VendorName,
        q.QuotedPrice, q.EstimatedDeliveryDays, q.ReliabilityRating,
        q.IsComplianceApproved, q.Notes, q.CreatedAt
    );
}