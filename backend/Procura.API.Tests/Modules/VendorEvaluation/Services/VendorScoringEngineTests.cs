using System;
using System.Collections.Generic;
using System.Linq;
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Enums;
using Procura.API.Modules.VendorEvaluation.Services;
using Xunit;

namespace Procura.API.Tests.Modules.VendorEvaluation.Services
{
    public class VendorScoringEngineTests
    {
        private readonly VendorScoringEngine _engine;

        public VendorScoringEngineTests()
        {
            _engine = new VendorScoringEngine();
        }

        [Fact]
        public void EvaluateCandidates_EmptyList_ReturnsEmptyResults()
        {
            // Act
            var results = _engine.EvaluateCandidates(Guid.NewGuid(), new List<CandidateVendorMetricDto>(), null, null, null);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void EvaluateCandidates_StandardCandidates_CalculatesWeightedScore()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var vendorId1 = Guid.NewGuid();
            var vendorId2 = Guid.NewGuid();

            var candidates = new List<CandidateVendorMetricDto>
            {
                new CandidateVendorMetricDto
                {
                    VendorId = vendorId1,
                    VendorName = "Acme Supplies",
                    QuotedPrice = 1000m,
                    EstimatedDeliveryDays = 5,
                    ReliabilityRating = 90m,
                    IsComplianceApproved = true
                },
                new CandidateVendorMetricDto
                {
                    VendorId = vendorId2,
                    VendorName = "Beta Global",
                    QuotedPrice = 2000m,
                    EstimatedDeliveryDays = 10,
                    ReliabilityRating = 80m,
                    IsComplianceApproved = true
                }
            };

            // Act
            var results = _engine.EvaluateCandidates(requestId, candidates, estimatedBudget: 1500m, requiredDeliveryDays: 7, customWeights: null);

            // Assert
            Assert.Equal(2, results.Count);

            var candidate1 = results.First(r => r.VendorId == vendorId1);
            var candidate2 = results.First(r => r.VendorId == vendorId2);

            Assert.True(candidate1.OverallScore > candidate2.OverallScore);
            Assert.Empty(candidate1.RiskFlags);
            Assert.Contains(candidate2.RiskFlags, r => r.Contains("PRICE_EXCEEDS_BUDGET"));
            Assert.Contains(candidate2.RiskFlags, r => r.Contains("DELIVERY_EXCEEDS_REQUIREMENT"));
        }

        [Fact]
        public void EvaluateCandidates_LowReliabilityAndNonCompliant_GeneratesRiskFlags()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var candidates = new List<CandidateVendorMetricDto>
            {
                new CandidateVendorMetricDto
                {
                    VendorId = vendorId,
                    VendorName = "Risky Corp",
                    QuotedPrice = 500m,
                    EstimatedDeliveryDays = 3,
                    ReliabilityRating = 45m, // Low (< 60)
                    IsComplianceApproved = false // Non-compliant
                }
            };

            // Act
            var results = _engine.EvaluateCandidates(requestId, candidates, null, null, null);

            // Assert
            Assert.Single(results);
            var result = results.First();

            Assert.Contains(result.RiskFlags, r => r.Contains("LOW_RELIABILITY_SCORE"));
            Assert.Contains(result.RiskFlags, r => r.Contains("NON_COMPLIANT_VENDOR"));
        }

        [Fact]
        public void EvaluateCandidates_CustomWeights_NormalizesAndAppliesProperly()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var candidates = new List<CandidateVendorMetricDto>
            {
                new CandidateVendorMetricDto
                {
                    VendorId = vendorId,
                    VendorName = "Reliable Tech",
                    QuotedPrice = 1000m,
                    EstimatedDeliveryDays = 5,
                    ReliabilityRating = 100m,
                    IsComplianceApproved = true
                }
            };

            var customWeights = new List<CriterionWeightConfigDto>
            {
                new CriterionWeightConfigDto { Criterion = EvaluationCriterionType.PRICE, Weight = 0.50m },
                new CriterionWeightConfigDto { Criterion = EvaluationCriterionType.DELIVERY_TIME, Weight = 0.10m },
                new CriterionWeightConfigDto { Criterion = EvaluationCriterionType.RELIABILITY, Weight = 0.20m },
                new CriterionWeightConfigDto { Criterion = EvaluationCriterionType.COMPLIANCE, Weight = 0.20m }
            };

            // Act
            var results = _engine.EvaluateCandidates(requestId, candidates, null, null, customWeights);

            // Assert
            Assert.Single(results);
            var result = results.First();

            var priceScore = result.CriterionScores.First(c => c.CriterionName == EvaluationCriterionType.PRICE);
            Assert.Equal(0.50m, priceScore.Weight);
        }

        [Fact]
        public void EvaluateCandidates_FiveStarReliabilityRating_NormalizesToPercentage()
        {
            // Arrange: candidate with 4.5 star rating on 1-5 scale (should normalize to 90/100 without LOW_RELIABILITY risk)
            var requestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var candidates = new List<CandidateVendorMetricDto>
            {
                new CandidateVendorMetricDto
                {
                    VendorId = vendorId,
                    VendorName = "Five Star Vendor",
                    QuotedPrice = 1000m,
                    EstimatedDeliveryDays = 5,
                    ReliabilityRating = 4.5m, // 4.5 out of 5 -> 90%
                    IsComplianceApproved = true
                }
            };

            // Act
            var results = _engine.EvaluateCandidates(requestId, candidates, null, null, null);

            // Assert
            Assert.Single(results);
            var result = results.First();
            var reliabilityCrit = result.CriterionScores.First(c => c.CriterionName == EvaluationCriterionType.RELIABILITY);

            Assert.Equal(90.00m, reliabilityCrit.Score);
            Assert.DoesNotContain(result.RiskFlags, r => r.Contains("LOW_RELIABILITY_SCORE"));
        }

        [Fact]
        public void EvaluateCandidates_SingleCandidate_EvaluatesSuccessfullyWithFullScore()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var candidates = new List<CandidateVendorMetricDto>
            {
                new CandidateVendorMetricDto
                {
                    VendorId = vendorId,
                    VendorName = "Solo Vendor",
                    QuotedPrice = 2500m,
                    EstimatedDeliveryDays = 7,
                    ReliabilityRating = 95m,
                    IsComplianceApproved = true
                }
            };

            // Act
            var results = _engine.EvaluateCandidates(requestId, candidates, estimatedBudget: 3000m, requiredDeliveryDays: 10, customWeights: null);

            // Assert
            Assert.Single(results);
            var result = results.First();
            Assert.True(result.OverallScore > 80m);
            Assert.Empty(result.RiskFlags);
        }

        [Fact]
        public void EvaluateCandidates_ZeroPriceCandidate_ReceivesZeroPriceScoreWithoutDividingByZero()
        {
            // Arrange: candidate with price = 0
            var requestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var candidates = new List<CandidateVendorMetricDto>
            {
                new CandidateVendorMetricDto
                {
                    VendorId = vendorId,
                    VendorName = "Free Sample Vendor",
                    QuotedPrice = 0m,
                    EstimatedDeliveryDays = 3,
                    ReliabilityRating = 80m,
                    IsComplianceApproved = true
                }
            };

            // Act
            var results = _engine.EvaluateCandidates(requestId, candidates, null, null, null);

            // Assert
            Assert.Single(results);
            var priceCrit = results.First().CriterionScores.First(c => c.CriterionName == EvaluationCriterionType.PRICE);
            Assert.Equal(0m, priceCrit.Score);
        }

        [Fact]
        public void EvaluateCandidates_NonCompliantCandidate_ReceivesZeroComplianceScoreAndRiskFlag()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var candidates = new List<CandidateVendorMetricDto>
            {
                new CandidateVendorMetricDto
                {
                    VendorId = vendorId,
                    VendorName = "Non Compliant Vendor",
                    QuotedPrice = 1000m,
                    EstimatedDeliveryDays = 5,
                    ReliabilityRating = 90m,
                    IsComplianceApproved = false
                }
            };

            // Act
            var results = _engine.EvaluateCandidates(requestId, candidates, null, null, null);

            // Assert
            Assert.Single(results);
            var complianceCrit = results.First().CriterionScores.First(c => c.CriterionName == EvaluationCriterionType.COMPLIANCE);
            Assert.Equal(0m, complianceCrit.Score);
            Assert.Contains(results.First().RiskFlags, r => r.Contains("NON_COMPLIANT_VENDOR"));
        }
    }
}
