using Procura.API.Modules.VendorEvaluation.DTOs;

namespace Procura.API.Modules.VendorEvaluation.Services;

public interface IVendorQuoteService
{
    Task<VendorQuoteResponseDto> SubmitQuoteAsync(CreateVendorQuoteDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VendorQuoteResponseDto>> GetQuotesByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<VendorQuoteResponseDto?> GetQuoteByIdAsync(Guid id, CancellationToken cancellationToken = default);
}