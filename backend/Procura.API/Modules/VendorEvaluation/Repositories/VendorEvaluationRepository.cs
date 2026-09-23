using Microsoft.EntityFrameworkCore;
using Procura.API.Modules.VendorEvaluation.Entities;
using Procura.API.Shared.Data;

namespace Procura.API.Modules.VendorEvaluation.Repositories;

/// <summary>
/// Entity Framework Core implementation of the IVendorEvaluationRepository.
/// </summary>
public class VendorEvaluationRepository : IVendorEvaluationRepository
{
    private readonly ApplicationDbContext _context;

    public VendorEvaluationRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Entities.VendorEvaluation?> GetByIdAsync(Guid id, bool includeCriterionScores = true, CancellationToken cancellationToken = default)
    {
        IQueryable<Entities.VendorEvaluation> query = _context.VendorEvaluations;

        if (includeCriterionScores)
        {
            query = query.Include(e => e.CriterionScores);
        }

        return await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Entities.VendorEvaluation>> GetByProcurementRequestIdAsync(Guid procurementRequestId, CancellationToken cancellationToken = default)
    {
        return await _context.VendorEvaluations
            .Include(e => e.CriterionScores)
            .Where(e => e.ProcurementRequestId == procurementRequestId)
            .OrderBy(e => e.Rank)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Entities.VendorEvaluation>> GetByVendorIdAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        return await _context.VendorEvaluations
            .Include(e => e.CriterionScores)
            .Where(e => e.VendorId == vendorId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Entities.VendorEvaluation> AddAsync(Entities.VendorEvaluation evaluation, CancellationToken cancellationToken = default)
    {
        await _context.VendorEvaluations.AddAsync(evaluation, cancellationToken);
        return evaluation;
    }

    public async Task AddRangeAsync(IEnumerable<Entities.VendorEvaluation> evaluations, CancellationToken cancellationToken = default)
    {
        await _context.VendorEvaluations.AddRangeAsync(evaluations, cancellationToken);
    }

    public Task UpdateAsync(Entities.VendorEvaluation evaluation, CancellationToken cancellationToken = default)
    {
        evaluation.UpdatedAt = DateTime.UtcNow;
        _context.VendorEvaluations.Update(evaluation);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Entities.VendorEvaluation evaluation, CancellationToken cancellationToken = default)
    {
        _context.VendorEvaluations.Remove(evaluation);
        return Task.CompletedTask;
    }

    public async Task DeleteByProcurementRequestIdAsync(Guid procurementRequestId, CancellationToken cancellationToken = default)
    {
        var evaluations = await _context.VendorEvaluations
            .Where(e => e.ProcurementRequestId == procurementRequestId)
            .ToListAsync(cancellationToken);

        if (evaluations.Count != 0)
        {
            _context.VendorEvaluations.RemoveRange(evaluations);
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
