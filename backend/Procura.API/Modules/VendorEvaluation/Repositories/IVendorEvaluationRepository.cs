using Procura.API.Modules.VendorEvaluation.Entities;

namespace Procura.API.Modules.VendorEvaluation.Repositories;

/// <summary>
/// Repository contract for managing VendorEvaluation and VendorEvaluationCriterionScore persistence.
/// </summary>
public interface IVendorEvaluationRepository
{
    /// <summary>
    /// Gets a single VendorEvaluation by its primary key ID.
    /// </summary>
    Task<Entities.VendorEvaluation?> GetByIdAsync(Guid id, bool includeCriterionScores = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all evaluations associated with a given ProcurementRequest ID, ordered by rank.
    /// </summary>
    Task<IReadOnlyList<Entities.VendorEvaluation>> GetByProcurementRequestIdAsync(Guid procurementRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all evaluations for a specific Vendor across all ProcurementRequests.
    /// </summary>
    Task<IReadOnlyList<Entities.VendorEvaluation>> GetByVendorIdAsync(Guid vendorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new VendorEvaluation record.
    /// </summary>
    Task<Entities.VendorEvaluation> AddAsync(Entities.VendorEvaluation evaluation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a collection of VendorEvaluations in a single batch.
    /// </summary>
    Task AddRangeAsync(IEnumerable<Entities.VendorEvaluation> evaluations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing VendorEvaluation.
    /// </summary>
    Task UpdateAsync(Entities.VendorEvaluation evaluation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an evaluation by entity.
    /// </summary>
    Task DeleteAsync(Entities.VendorEvaluation evaluation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all existing evaluations for a given procurement request (useful when re-running evaluation).
    /// </summary>
    Task DeleteByProcurementRequestIdAsync(Guid procurementRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists pending changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
