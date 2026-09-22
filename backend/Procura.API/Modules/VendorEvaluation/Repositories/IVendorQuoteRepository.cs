using Procura.API.Modules.VendorEvaluation.Entities;

namespace Procura.API.Modules.VendorEvaluation.Repositories;

public interface IVendorQuoteRepository
{
    Task<VendorQuote> AddAsync(VendorQuote quote, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VendorQuote>> GetByProcurementRequestIdAsync(Guid procurementRequestId, CancellationToken cancellationToken = default);
    Task<VendorQuote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}