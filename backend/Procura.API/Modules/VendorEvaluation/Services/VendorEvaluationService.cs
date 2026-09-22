using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Procura.API.Modules.ProcurementRequest.Repositories;
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Entities;
using Procura.API.Modules.VendorEvaluation.Repositories;
using Procura.API.Shared.Data;

namespace Procura.API.Modules.VendorEvaluation.Services;

/// <summary>
/// Core business service implementing evaluation orchestration, ranking, recommendation generation,
/// and evaluation lifecycle management.
/// </summary>
public class VendorEvaluationService : IVendorEvaluationService
{
    private readonly IVendorEvaluationRepository _repository;
    private readonly IVendorQuoteRepository _quoteRepository;
    private readonly ApplicationDbContext _dbContext;
    private readonly IVendorScoringEngine _scoringEngine;
    private readonly IProcurementRequestRepository _procurementRequestRepository;
    private readonly ILogger<VendorEvaluationService> _logger;

    public VendorEvaluationService(
        IVendorEvaluationRepository repository,
        IVendorQuoteRepository quoteRepository,
        ApplicationDbContext dbContext,
        IVendorScoringEngine scoringEngine,
        IProcurementRequestRepository procurementRequestRepository,
        ILogger<VendorEvaluationService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _quoteRepository = quoteRepository ?? throw new ArgumentNullException(nameof(quoteRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _scoringEngine = scoringEngine ?? throw new ArgumentNullException(nameof(scoringEngine));
        _procurementRequestRepository = procurementRequestRepository ?? throw new ArgumentNullException(nameof(procurementRequestRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }


    public async Task<ProcurementEvaluationSummaryDto> EvaluateAndRankCandidateVendorsAsync(
        EvaluateVendorsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting evaluation for ProcurementRequest {ProcurementRequestId}",
            request.ProcurementRequestId);

        // Initialize empty list
        var candidateMetrics = new List<CandidateVendorMetricDto>();

        // If explicit candidate vendors are provided in the payload, use them. Otherwise, fall back to querying submitted quotes from DB.
        if (request.CandidateVendors != null && request.CandidateVendors.Any())
        {
            _logger.LogInformation("Using {Count} explicit candidate vendor(s) provided in request payload for ProcurementRequest {ProcurementRequestId}",
                request.CandidateVendors.Count, request.ProcurementRequestId);
            
            candidateMetrics = request.CandidateVendors;
        }
        else
        {
            _logger.LogInformation("No candidate vendors provided in request payload; loading vendor quotes from database for ProcurementRequest {ProcurementRequestId}",
                request.ProcurementRequestId);

           
            // 1. Load submitted vendor quotes via the quote repository
            var quotes = await _quoteRepository.GetByProcurementRequestIdAsync(request.ProcurementRequestId, cancellationToken);

            if (!quotes.Any())
            {
                _logger.LogWarning("No vendor quotes found in database for ProcurementRequest {ProcurementRequestId}", request.ProcurementRequestId);
                return new ProcurementEvaluationSummaryDto
                {
                    ProcurementRequestId = request.ProcurementRequestId,
                    TotalCandidatesEvaluated = 0,
                    TopRecommendedVendorId = null,
                    TopScore = null,
                    RecommendationSummary = "No vendor quotes found to evaluate.",
                    EvaluatedAt = DateTime.UtcNow,
                    RankedEvaluations = new List<VendorEvaluationResponseDto>()
                };
            }

            // 2. Map stored VendorQuote entities into CandidateVendorMetricDto objects expected by VendorScoringEngine
            candidateMetrics = quotes.Select(q => new CandidateVendorMetricDto
            {
                VendorId = q.VendorId,
                QuotedPrice = q.QuotedPrice,
                EstimatedDeliveryDays = q.EstimatedDeliveryDays,
                ReliabilityRating = q.ReliabilityRating,
                IsComplianceApproved = q.IsComplianceApproved,
                KnownRisks = !string.IsNullOrWhiteSpace(q.Notes) ? new List<string> { q.Notes } : new List<string>()
            }).ToList();
        }

        // 3. Determine budget and delivery days, auto-populating from ProcurementRequest if not explicitly passed
        var budget = request.EstimatedBudget;
        var deliveryDays = request.RequiredDeliveryDays;

        if (!budget.HasValue || !deliveryDays.HasValue)
        {
            var procurementRequest = await _procurementRequestRepository.GetByIdAsync(request.ProcurementRequestId);
            if (procurementRequest != null)
            {
                if (!budget.HasValue && procurementRequest.EstimatedTotal > 0)
                {
                    budget = procurementRequest.EstimatedTotal;
                    _logger.LogInformation("Auto-populated EstimatedBudget={Budget} from ProcurementRequest {Id}", budget, request.ProcurementRequestId);
                }

                if (!deliveryDays.HasValue)
                {
                    var daysRemaining = (procurementRequest.RequiredByDate.Date - DateTime.UtcNow.Date).Days;
                    if (daysRemaining > 0)
                    {
                        deliveryDays = daysRemaining;
                        _logger.LogInformation("Auto-calculated RequiredDeliveryDays={Days} from RequiredByDate {Date}", deliveryDays, procurementRequest.RequiredByDate);
                    }
                }
            }
        }

        // 4. Run scoring engine
        var evaluationResults = _scoringEngine.EvaluateCandidates(
            request.ProcurementRequestId,
            candidateMetrics,
            budget,
            deliveryDays,
            request.CustomWeights);

        // 5. Rank candidates by overall score in descending order
        var sortedResults = evaluationResults
            .OrderByDescending(r => r.OverallScore)
            .ToList();

        // 6. Clear any prior evaluations for this procurement request to ensure idempotent re-evaluations
        await _repository.DeleteByProcurementRequestIdAsync(request.ProcurementRequestId, cancellationToken);

        // 7. Transform into persistent entities with assigned ranks
        var evaluationEntities = new List<Entities.VendorEvaluation>();
        int currentRank = 1;

        foreach (var result in sortedResults)
        {
            var evaluationId = Guid.NewGuid();

            var entity = new Entities.VendorEvaluation
            {
                Id = evaluationId,
                ProcurementRequestId = request.ProcurementRequestId,
                VendorId = result.VendorId,
                Rank = currentRank++,
                OverallScore = result.OverallScore,
                Reasoning = result.Reasoning,
                RiskFlags = result.RiskFlags,
                GeneratedByAgent = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CriterionScores = result.CriterionScores.Select(c => new VendorEvaluationCriterionScore
                {
                    Id = Guid.NewGuid(),
                    VendorEvaluationId = evaluationId,
                    CriterionName = c.CriterionName,
                    Score = c.Score,
                    Weight = c.Weight
                }).ToList()
            };

            evaluationEntities.Add(entity);
        }

        // 8. Persist batch to repository
        await _repository.AddRangeAsync(evaluationEntities, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully persisted {Count} evaluations for ProcurementRequest {ProcurementRequestId}",
            evaluationEntities.Count, request.ProcurementRequestId);

        // 9. Build recommendation summary response
        var responseDtos = evaluationEntities.Select(MapToResponseDto).ToList();
        var topRecommendation = responseDtos.FirstOrDefault();

        string summaryText = topRecommendation != null
            ? $"Top recommended candidate is Vendor {topRecommendation.VendorId} (Rank #1) with an overall score of {topRecommendation.OverallScore:F2}/100. Evaluated {evaluationEntities.Count} candidate(s) in total."
            : "No candidates evaluated.";

        return new ProcurementEvaluationSummaryDto
        {
            ProcurementRequestId = request.ProcurementRequestId,
            TotalCandidatesEvaluated = evaluationEntities.Count,
            TopRecommendedVendorId = topRecommendation?.VendorId,
            TopScore = topRecommendation?.OverallScore,
            RecommendationSummary = summaryText,
            EvaluatedAt = DateTime.UtcNow,
            RankedEvaluations = responseDtos
        };
    }

    public async Task<IReadOnlyList<VendorEvaluationResponseDto>> GetEvaluationsByProcurementRequestIdAsync(
        Guid procurementRequestId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByProcurementRequestIdAsync(procurementRequestId, cancellationToken);
        return entities.Select(MapToResponseDto).ToList();
    }

    public async Task<VendorEvaluationResponseDto?> GetEvaluationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, includeCriterionScores: true, cancellationToken);
        return entity != null ? MapToResponseDto(entity) : null;
    }

    public async Task<IReadOnlyList<VendorEvaluationResponseDto>> GetEvaluationsByVendorIdAsync(
        Guid vendorId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByVendorIdAsync(vendorId, cancellationToken);
        return entities.Select(MapToResponseDto).ToList();
    }

    public async Task<VendorEvaluationResponseDto> CreateEvaluationAsync(
        CreateVendorEvaluationDto dto,
        CancellationToken cancellationToken = default)
    {
        var evaluationId = Guid.NewGuid();
        var entity = new Entities.VendorEvaluation
        {
            Id = evaluationId,
            ProcurementRequestId = dto.ProcurementRequestId,
            VendorId = dto.VendorId,
            Rank = dto.Rank,
            OverallScore = dto.OverallScore,
            Reasoning = dto.Reasoning,
            RiskFlags = dto.RiskFlags ?? new List<string>(),
            GeneratedByAgent = dto.GeneratedByAgent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CriterionScores = dto.CriterionScores.Select(c => new VendorEvaluationCriterionScore
            {
                Id = Guid.NewGuid(),
                VendorEvaluationId = evaluationId,
                CriterionName = c.CriterionName,
                Score = c.Score,
                Weight = c.Weight
            }).ToList()
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return MapToResponseDto(entity);
    }

    public async Task<VendorEvaluationResponseDto?> UpdateEvaluationAsync(
        Guid id,
        UpdateVendorEvaluationDto dto,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, includeCriterionScores: true, cancellationToken);
        if (entity == null)
        {
            return null;
        }

        // 1. Update basic fields
        if (dto.Rank.HasValue) entity.Rank = dto.Rank.Value;
        if (dto.OverallScore.HasValue) entity.OverallScore = dto.OverallScore.Value;
        if (dto.Reasoning != null) entity.Reasoning = dto.Reasoning;
        if (dto.RiskFlags != null) entity.RiskFlags = dto.RiskFlags;

        // 2. Delete old criterion scores directly via the DbSet, add new ones directly via the DbSet
        if (dto.CriterionScores != null)
        {
            _dbContext.VendorEvaluationCriterionScores.RemoveRange(entity.CriterionScores);

            var newScores = dto.CriterionScores.Select(c => new VendorEvaluationCriterionScore
            {
                Id = Guid.NewGuid(),
                VendorEvaluationId = entity.Id,
                CriterionName = c.CriterionName,
                Score = c.Score,
                Weight = c.Weight
            }).ToList();

            await _dbContext.VendorEvaluationCriterionScores.AddRangeAsync(newScores, cancellationToken);
            entity.CriterionScores = newScores;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken);

        return MapToResponseDto(entity);
    }

    public async Task<bool> DeleteEvaluationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, includeCriterionScores: false, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        await _repository.DeleteAsync(entity, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ProcurementEvaluationSummaryDto?> GetRecommendationSummaryAsync(
        Guid procurementRequestId,
        CancellationToken cancellationToken = default)
    {
        var evaluations = await _repository.GetByProcurementRequestIdAsync(procurementRequestId, cancellationToken);
        if (evaluations.Count == 0)
        {
            return null;
        }

        var responseDtos = evaluations.Select(MapToResponseDto).ToList();
        var topRecommendation = responseDtos.FirstOrDefault();

        string summary = topRecommendation != null
            ? $"Top recommended candidate is Vendor {topRecommendation.VendorId} (Rank #1) with overall score {topRecommendation.OverallScore:F2}/100. Total {responseDtos.Count} candidate(s) evaluated."
            : "No evaluations found.";

        return new ProcurementEvaluationSummaryDto
        {
            ProcurementRequestId = procurementRequestId,
            TotalCandidatesEvaluated = responseDtos.Count,
            TopRecommendedVendorId = topRecommendation?.VendorId,
            TopScore = topRecommendation?.OverallScore,
            RecommendationSummary = summary,
            EvaluatedAt = evaluations.Max(e => e.UpdatedAt),
            RankedEvaluations = responseDtos
        };
    }

    private static VendorEvaluationResponseDto MapToResponseDto(Entities.VendorEvaluation entity)
    {
        return new VendorEvaluationResponseDto
        {
            Id = entity.Id,
            ProcurementRequestId = entity.ProcurementRequestId,
            VendorId = entity.VendorId,
            Rank = entity.Rank,
            OverallScore = entity.OverallScore,
            Reasoning = entity.Reasoning,
            RiskFlags = entity.RiskFlags ?? new List<string>(),
            GeneratedByAgent = entity.GeneratedByAgent,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CriterionScores = entity.CriterionScores?.Select(c => new CriterionScoreResponseDto
            {
                Id = c.Id,
                VendorEvaluationId = c.VendorEvaluationId,
                CriterionName = c.CriterionName,
                Score = c.Score,
                Weight = c.Weight
            }).ToList() ?? new List<CriterionScoreResponseDto>()
        };
    }

}