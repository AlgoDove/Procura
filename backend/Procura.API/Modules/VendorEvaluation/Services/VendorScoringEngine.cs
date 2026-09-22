using System.Text;
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Entities;
using Procura.API.Modules.VendorEvaluation.Enums;

namespace Procura.API.Modules.VendorEvaluation.Services;

/// <summary>
/// Scoring engine implementation applying standardized weighting, normalization,
/// risk detection, and natural reasoning generation.
/// </summary>
public class VendorScoringEngine : IVendorScoringEngine
{
    private static readonly Dictionary<EvaluationCriterionType, decimal> DefaultWeights = new()
    {
        { EvaluationCriterionType.PRICE, 0.35m },
        { EvaluationCriterionType.DELIVERY_TIME, 0.25m },
        { EvaluationCriterionType.RELIABILITY, 0.25m },
        { EvaluationCriterionType.COMPLIANCE, 0.15m }
    };

    public IReadOnlyDictionary<EvaluationCriterionType, decimal> GetDefaultWeights()
    {
        return DefaultWeights;
    }

    public IReadOnlyList<CandidateEvaluationResult> EvaluateCandidates(
        Guid procurementRequestId,
        IReadOnlyList<CandidateVendorMetricDto> candidates,
        decimal? estimatedBudget,
        int? requiredDeliveryDays,
        IReadOnlyList<CriterionWeightConfigDto>? customWeights)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return Array.Empty<CandidateEvaluationResult>();
        }

        var weights = ResolveWeights(customWeights);
        var minPrice = candidates.Where(c => c.QuotedPrice > 0).Select(c => c.QuotedPrice).DefaultIfEmpty(1m).Min();
        var minDeliveryDays = candidates.Where(c => c.EstimatedDeliveryDays > 0).Select(c => c.EstimatedDeliveryDays).DefaultIfEmpty(1).Min();

        var results = new List<CandidateEvaluationResult>();

        foreach (var candidate in candidates)
        {
            var riskFlags = new List<string>();
            var criterionScores = new List<VendorEvaluationCriterionScore>();

            // 1. PRICE CRITERION
            decimal priceScore = 0;
            if (candidate.QuotedPrice > 0 && minPrice > 0)
            {
                priceScore = Math.Round(Math.Min(100m, (minPrice / candidate.QuotedPrice) * 100m), 2);
            }
            if (estimatedBudget.HasValue && candidate.QuotedPrice > estimatedBudget.Value)
            {
                var overBudgetPercent = Math.Round(((candidate.QuotedPrice - estimatedBudget.Value) / estimatedBudget.Value) * 100m, 1);
                riskFlags.Add($"PRICE_EXCEEDS_BUDGET: Quoted price of {candidate.QuotedPrice:C} exceeds estimated budget of {estimatedBudget.Value:C} by {overBudgetPercent}%.");
            }
            criterionScores.Add(new VendorEvaluationCriterionScore
            {
                Id = Guid.NewGuid(),
                CriterionName = EvaluationCriterionType.PRICE,
                Score = priceScore,
                Weight = weights[EvaluationCriterionType.PRICE]
            });

            // 2. DELIVERY TIME CRITERION
            decimal deliveryScore = 0;
            if (requiredDeliveryDays.HasValue && requiredDeliveryDays.Value > 0)
            {
                if (candidate.EstimatedDeliveryDays <= requiredDeliveryDays.Value)
                {
                    // Full delivery points with slight bonus for faster delivery
                    decimal ratio = (decimal)candidate.EstimatedDeliveryDays / requiredDeliveryDays.Value;
                    deliveryScore = Math.Round(100m - (ratio * 15m), 2);
                }
                else
                {
                    int delayDays = candidate.EstimatedDeliveryDays - requiredDeliveryDays.Value;
                    deliveryScore = Math.Max(0m, Math.Round(80m - (delayDays * 10m), 2));
                    riskFlags.Add($"DELIVERY_EXCEEDS_REQUIREMENT: Estimated delivery of {candidate.EstimatedDeliveryDays} days exceeds required timeline of {requiredDeliveryDays.Value} days by {delayDays} days.");
                }
            }
            else
            {
                deliveryScore = candidate.EstimatedDeliveryDays > 0
                    ? Math.Round(Math.Min(100m, ((decimal)minDeliveryDays / candidate.EstimatedDeliveryDays) * 100m), 2)
                    : 100m;
            }
            criterionScores.Add(new VendorEvaluationCriterionScore
            {
                Id = Guid.NewGuid(),
                CriterionName = EvaluationCriterionType.DELIVERY_TIME,
                Score = deliveryScore,
                Weight = weights[EvaluationCriterionType.DELIVERY_TIME]
            });

            // 3. RELIABILITY CRITERION
            decimal reliabilityScore = Math.Clamp(Math.Round(candidate.ReliabilityRating, 2), 0m, 100m);
            if (reliabilityScore < 60m)
            {
                riskFlags.Add($"LOW_RELIABILITY_SCORE: Reliability rating is {reliabilityScore}%, which is below the minimum recommended 60% threshold.");
            }
            criterionScores.Add(new VendorEvaluationCriterionScore
            {
                Id = Guid.NewGuid(),
                CriterionName = EvaluationCriterionType.RELIABILITY,
                Score = reliabilityScore,
                Weight = weights[EvaluationCriterionType.RELIABILITY]
            });

            // 4. COMPLIANCE CRITERION
            decimal complianceScore = candidate.IsComplianceApproved ? 100m : 0m;
            if (!candidate.IsComplianceApproved)
            {
                riskFlags.Add("NON_COMPLIANT_VENDOR: Vendor is not compliance-approved by the Compliance team.");
            }
            criterionScores.Add(new VendorEvaluationCriterionScore
            {
                Id = Guid.NewGuid(),
                CriterionName = EvaluationCriterionType.COMPLIANCE,
                Score = complianceScore,
                Weight = weights[EvaluationCriterionType.COMPLIANCE]
            });

            // Include any known external risks
            if (candidate.KnownRisks != null && candidate.KnownRisks.Count > 0)
            {
                riskFlags.AddRange(candidate.KnownRisks.Where(r => !string.IsNullOrWhiteSpace(r)));
            }

            // OVERALL WEIGHTED SCORE CALCULATION
            decimal overallScore = Math.Round(criterionScores.Sum(c => c.Score * c.Weight), 2);

            // GENERATE STRUCTURED REASONING
            string reasoning = GenerateReasoning(candidate, overallScore, criterionScores, riskFlags, minPrice);

            results.Add(new CandidateEvaluationResult
            {
                VendorId = candidate.VendorId,
                OverallScore = overallScore,
                Reasoning = reasoning,
                RiskFlags = riskFlags,
                CriterionScores = criterionScores
            });
        }

        return results;
    }

    private static Dictionary<EvaluationCriterionType, decimal> ResolveWeights(IReadOnlyList<CriterionWeightConfigDto>? customWeights)
    {
        if (customWeights == null || customWeights.Count == 0)
        {
            return new Dictionary<EvaluationCriterionType, decimal>(DefaultWeights);
        }

        var weights = new Dictionary<EvaluationCriterionType, decimal>(DefaultWeights);
        foreach (var custom in customWeights)
        {
            if (weights.ContainsKey(custom.Criterion) && custom.Weight >= 0)
            {
                weights[custom.Criterion] = custom.Weight;
            }
        }

        // Normalize weights to sum up to 1.0
        decimal total = weights.Values.Sum();
        if (total > 0)
        {
            foreach (var key in weights.Keys.ToList())
            {
                weights[key] = Math.Round(weights[key] / total, 3);
            }
        }

        return weights;
    }

    private static string GenerateReasoning(
        CandidateVendorMetricDto candidate,
        decimal overallScore,
        List<VendorEvaluationCriterionScore> criteria,
        List<string> risks,
        decimal minPrice)
    {
        var sb = new StringBuilder();
        var vendorLabel = string.IsNullOrWhiteSpace(candidate.VendorName) ? $"Vendor {candidate.VendorId}" : candidate.VendorName;

        sb.Append($"{vendorLabel} achieved an overall evaluation score of {overallScore:F2}/100. ");

        var priceCrit = criteria.FirstOrDefault(c => c.CriterionName == EvaluationCriterionType.PRICE);
        var deliveryCrit = criteria.FirstOrDefault(c => c.CriterionName == EvaluationCriterionType.DELIVERY_TIME);
        var reliabilityCrit = criteria.FirstOrDefault(c => c.CriterionName == EvaluationCriterionType.RELIABILITY);
        var complianceCrit = criteria.FirstOrDefault(c => c.CriterionName == EvaluationCriterionType.COMPLIANCE);

        if (candidate.QuotedPrice == minPrice && minPrice > 0)
        {
            sb.Append("Offered the most competitive pricing among candidates. ");
        }
        else if (priceCrit != null)
        {
            sb.Append($"Pricing score: {priceCrit.Score:F1}/100 ({candidate.QuotedPrice:C}). ");
        }

        if (deliveryCrit != null)
        {
            sb.Append($"Delivery timeline score: {deliveryCrit.Score:F1}/100 ({candidate.EstimatedDeliveryDays} days). ");
        }

        if (reliabilityCrit != null)
        {
            sb.Append($"Reliability rating score: {reliabilityCrit.Score:F1}/100. ");
        }

        if (candidate.IsComplianceApproved)
        {
            sb.Append("Compliance status is verified and approved. ");
        }
        else
        {
            sb.Append("Vendor is NOT compliance-approved. ");
        }

        if (risks.Count > 0)
        {
            sb.Append($"Identified {risks.Count} risk flag(s) requiring consideration.");
        }
        else
        {
            sb.Append("No immediate risk flags identified.");
        }

        return sb.ToString();
    }
}
