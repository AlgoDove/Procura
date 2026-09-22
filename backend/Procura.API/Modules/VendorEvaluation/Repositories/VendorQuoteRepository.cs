using Microsoft.EntityFrameworkCore;
using Procura.API.Modules.VendorEvaluation.Entities;
using Procura.API.Shared.Data;

namespace Procura.API.Modules.VendorEvaluation.Repositories;

public class VendorQuoteRepository : IVendorQuoteRepository
{
    private readonly ApplicationDbContext _context;

    public VendorQuoteRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<VendorQuote> AddAsync(VendorQuote quote, CancellationToken cancellationToken = default)
    {
        await _context.VendorQuotes.AddAsync(quote, cancellationToken);
        return quote;
    }

    public async Task<IReadOnlyList<VendorQuote>> GetByProcurementRequestIdAsync(Guid procurementRequestId, CancellationToken cancellationToken = default)
    {
        return await _context.VendorQuotes
            .Where(q => q.ProcurementRequestId == procurementRequestId)
            .ToListAsync(cancellationToken);
    }

    public async Task<VendorQuote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.VendorQuotes.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}