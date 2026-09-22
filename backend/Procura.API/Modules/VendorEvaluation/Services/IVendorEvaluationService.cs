using Procura.API.Modules.VendorEvaluation.DTOs;

namespace Procura.API.Modules.VendorEvaluation.Services;

/// <summary>
/// Service contract handling vendor evaluation workflows, ranking, retrieval, and recommendation summaries.
/// </summary>
public interface IVendorEvaluationService
{

    /// <summary>
    /// Executes automated evaluation & ranking for candidates against a procurement request and persists results.
    /// </summary>
    Task<ProcurementEvaluationSummaryDto> EvaluateAndRankCandidateVendorsAsync(
        EvaluateVendorsRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all evaluations for a specific ProcurementRequest ID, ordered by rank.
    /// </summary>
    Task<IReadOnlyList<VendorEvaluationResponseDto>> GetEvaluationsByProcurementRequestIdAsync(
        Guid procurementRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single evaluation by its ID.
    /// </summary>
    Task<VendorEvaluationResponseDto?> GetEvaluationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all evaluations for a specific Vendor across all procurement requests.
    /// </summary>
    Task<IReadOnlyList<VendorEvaluationResponseDto>> GetEvaluationsByVendorIdAsync(
        Guid vendorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a manual or external vendor evaluation record.
    /// </summary>
    Task<VendorEvaluationResponseDto> CreateEvaluationAsync(
        CreateVendorEvaluationDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing vendor evaluation record.
    /// </summary>
    Task<VendorEvaluationResponseDto?> UpdateEvaluationAsync(
        Guid id,
        UpdateVendorEvaluationDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an evaluation by ID.
    /// </summary>
    Task<bool> DeleteEvaluationAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates or retrieves the executive recommendation summary for a procurement request.
    /// </summary>
    Task<ProcurementEvaluationSummaryDto?> GetRecommendationSummaryAsync(
        Guid procurementRequestId,
        CancellationToken cancellationToken = default);
}
