using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Procura.API.Modules.ProcurementRequest.Entities;
using ProcurementRequestEntity = Procura.API.Modules.ProcurementRequest.Entities.ProcurementRequest;
using Procura.API.Modules.ProcurementRequest.Repositories;
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Entities;
using VendorEvaluationEntity = Procura.API.Modules.VendorEvaluation.Entities.VendorEvaluation;
using Procura.API.Modules.VendorEvaluation.Enums;
using Procura.API.Modules.VendorEvaluation.Repositories;
using Procura.API.Modules.VendorEvaluation.Services;
using Procura.API.Shared.Data;
using Xunit;

namespace Procura.API.Tests.Modules.VendorEvaluation.Services
{
    public class VendorEvaluationServiceTests
    {
        private readonly Mock<IVendorEvaluationRepository> _repositoryMock;
        private readonly Mock<IVendorQuoteRepository> _quoteRepositoryMock;
        private readonly Mock<IVendorScoringEngine> _scoringEngineMock;
        private readonly ApplicationDbContext _dbContext;
        private readonly Mock<IProcurementRequestRepository> _procurementRequestRepositoryMock;
        private readonly Mock<ILogger<VendorEvaluationService>> _loggerMock;
        private readonly VendorEvaluationService _service;

        public VendorEvaluationServiceTests()
        {
            _repositoryMock = new Mock<IVendorEvaluationRepository>();
            _quoteRepositoryMock = new Mock<IVendorQuoteRepository>();
            _scoringEngineMock = new Mock<IVendorScoringEngine>();
            _procurementRequestRepositoryMock = new Mock<IProcurementRequestRepository>();
            _loggerMock = new Mock<ILogger<VendorEvaluationService>>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _dbContext = new ApplicationDbContext(dbOptions);

            _service = new VendorEvaluationService(
                _repositoryMock.Object,
                _quoteRepositoryMock.Object,
                _dbContext,
                _scoringEngineMock.Object,
                _procurementRequestRepositoryMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task EvaluateAndRankCandidateVendorsAsync_RanksAndPersistsCorrectly()
        {
            // Arrange
            var procurementRequestId = Guid.NewGuid();
            var vendor1Id = Guid.NewGuid();
            var vendor2Id = Guid.NewGuid();

            var request = new EvaluateVendorsRequestDto
            {
                ProcurementRequestId = procurementRequestId,
                CandidateVendors = new List<CandidateVendorMetricDto>
                {
                    new CandidateVendorMetricDto { VendorId = vendor1Id, QuotedPrice = 100 },
                    new CandidateVendorMetricDto { VendorId = vendor2Id, QuotedPrice = 200 }
                }
            };

            var scoredResults = new List<CandidateEvaluationResult>
            {
                new CandidateEvaluationResult
                {
                    VendorId = vendor1Id,
                    OverallScore = 95.5m,
                    Reasoning = "Best score",
                    RiskFlags = new List<string>(),
                    CriterionScores = new List<VendorEvaluationCriterionScore>()
                },
                new CandidateEvaluationResult
                {
                    VendorId = vendor2Id,
                    OverallScore = 80.0m,
                    Reasoning = "Second best",
                    RiskFlags = new List<string>(),
                    CriterionScores = new List<VendorEvaluationCriterionScore>()
                }
            };

            _procurementRequestRepositoryMock
                .Setup(r => r.GetByIdAsync(procurementRequestId))
                .ReturnsAsync((ProcurementRequestEntity?)null);

            _scoringEngineMock
                .Setup(e => e.EvaluateCandidates(procurementRequestId, request.CandidateVendors, null, null, null))
                .Returns(scoredResults);

            _repositoryMock
                .Setup(r => r.DeleteByProcurementRequestIdAsync(procurementRequestId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _repositoryMock
                .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<VendorEvaluationEntity>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _repositoryMock
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            // Act
            var summary = await _service.EvaluateAndRankCandidateVendorsAsync(request);

            // Assert
            Assert.NotNull(summary);
            Assert.Equal(2, summary.TotalCandidatesEvaluated);
            Assert.Equal(vendor1Id, summary.TopRecommendedVendorId);
            Assert.Equal(95.5m, summary.TopScore);
            Assert.Equal(2, summary.RankedEvaluations.Count);
            Assert.Equal(1, summary.RankedEvaluations[0].Rank);
            Assert.Equal(2, summary.RankedEvaluations[1].Rank);

            _repositoryMock.Verify(r => r.DeleteByProcurementRequestIdAsync(procurementRequestId, It.IsAny<CancellationToken>()), Times.Once);
            _repositoryMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<VendorEvaluationEntity>>(), It.IsAny<CancellationToken>()), Times.Once);
            _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EvaluateAndRankCandidateVendorsAsync_AutoPopulatesBudgetAndDeliveryDaysFromProcurementRequest()
        {
            // Arrange
            var procurementRequestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var request = new EvaluateVendorsRequestDto
            {
                ProcurementRequestId = procurementRequestId,
                // EstimatedBudget and RequiredDeliveryDays omitted
                CandidateVendors = new List<CandidateVendorMetricDto>
                {
                    new CandidateVendorMetricDto { VendorId = vendorId, QuotedPrice = 4500 }
                }
            };

            var existingPR = new ProcurementRequestEntity
            {
                Id = procurementRequestId,
                EstimatedTotal = 5000m,
                RequiredByDate = DateTime.UtcNow.Date.AddDays(10),
                Title = "Test PR",
                Description = "Test",
                Justification = "Test",
                RequestNumber = "PR-1234"
            };

            _procurementRequestRepositoryMock
                .Setup(r => r.GetByIdAsync(procurementRequestId))
                .ReturnsAsync(existingPR);

            _scoringEngineMock
                .Setup(e => e.EvaluateCandidates(procurementRequestId, request.CandidateVendors, 5000m, 10, null))
                .Returns(new List<CandidateEvaluationResult>
                {
                    new CandidateEvaluationResult
                    {
                        VendorId = vendorId,
                        OverallScore = 90m,
                        Reasoning = "Within budget and delivery timeline",
                        RiskFlags = new List<string>(),
                        CriterionScores = new List<VendorEvaluationCriterionScore>()
                    }
                });

            _repositoryMock
                .Setup(r => r.DeleteByProcurementRequestIdAsync(procurementRequestId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _repositoryMock
                .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<VendorEvaluationEntity>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _repositoryMock
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            var summary = await _service.EvaluateAndRankCandidateVendorsAsync(request);

            // Assert
            Assert.NotNull(summary);
            _scoringEngineMock.Verify(e => e.EvaluateCandidates(procurementRequestId, request.CandidateVendors, 5000m, 10, null), Times.Once);
        }

        [Fact]
        public async Task GetRecommendationSummaryAsync_WhenEvaluationsExist_ReturnsTopVendorSummary()
        {
            // Arrange
            var procurementRequestId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();

            var evaluations = new List<VendorEvaluationEntity>
            {
                new VendorEvaluationEntity
                {
                    Id = Guid.NewGuid(),
                    ProcurementRequestId = procurementRequestId,
                    VendorId = vendorId,
                    Rank = 1,
                    OverallScore = 92.0m,
                    Reasoning = "Top vendor",
                    CriterionScores = new List<VendorEvaluationCriterionScore>(),
                    UpdatedAt = DateTime.UtcNow
                }
            };

            _repositoryMock
                .Setup(r => r.GetByProcurementRequestIdAsync(procurementRequestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(evaluations);

            // Act
            var summary = await _service.GetRecommendationSummaryAsync(procurementRequestId);

            // Assert
            Assert.NotNull(summary);
            Assert.Equal(vendorId, summary.TopRecommendedVendorId);
            Assert.Equal(92.0m, summary.TopScore);
            Assert.Single(summary.RankedEvaluations);
        }

        [Fact]
        public async Task GetRecommendationSummaryAsync_WhenNoEvaluations_ReturnsNull()
        {
            // Arrange
            var procurementRequestId = Guid.NewGuid();

            _repositoryMock
                .Setup(r => r.GetByProcurementRequestIdAsync(procurementRequestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VendorEvaluationEntity>());

            // Act
            var summary = await _service.GetRecommendationSummaryAsync(procurementRequestId);

            // Assert
            Assert.Null(summary);
        }

        [Fact]
        public async Task DeleteEvaluationAsync_WhenExists_ReturnsTrue()
        {
            // Arrange
            var id = Guid.NewGuid();
            var evaluation = new VendorEvaluationEntity { Id = id };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(id, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(evaluation);

            _repositoryMock
                .Setup(r => r.DeleteAsync(evaluation, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _repositoryMock
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            var result = await _service.DeleteEvaluationAsync(id);

            // Assert
            Assert.True(result);
            _repositoryMock.Verify(r => r.DeleteAsync(evaluation, It.IsAny<CancellationToken>()), Times.Once);
            _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EvaluateAndRankCandidateVendorsAsync_WhenScoresTied_RanksCheaperVendorFirst()
        {
            // Arrange: Vendor 1 ($1000) and Vendor 2 ($1500) have the exact same OverallScore = 85.0
            var procurementRequestId = Guid.NewGuid();
            var vendor1Id = Guid.NewGuid(); // cheaper
            var vendor2Id = Guid.NewGuid(); // more expensive

            var request = new EvaluateVendorsRequestDto
            {
                ProcurementRequestId = procurementRequestId,
                CandidateVendors = new List<CandidateVendorMetricDto>
                {
                    // Vendor 2 passed first in the list
                    new CandidateVendorMetricDto { VendorId = vendor2Id, QuotedPrice = 1500m, ReliabilityRating = 90m },
                    new CandidateVendorMetricDto { VendorId = vendor1Id, QuotedPrice = 1000m, ReliabilityRating = 90m }
                }
            };

            var scoredResults = new List<CandidateEvaluationResult>
            {
                new CandidateEvaluationResult
                {
                    VendorId = vendor2Id,
                    OverallScore = 85.0m,
                    Reasoning = "Vendor 2",
                    CriterionScores = new List<VendorEvaluationCriterionScore>()
                },
                new CandidateEvaluationResult
                {
                    VendorId = vendor1Id,
                    OverallScore = 85.0m,
                    Reasoning = "Vendor 1",
                    CriterionScores = new List<VendorEvaluationCriterionScore>()
                }
            };

            _procurementRequestRepositoryMock
                .Setup(r => r.GetByIdAsync(procurementRequestId))
                .ReturnsAsync((ProcurementRequestEntity?)null);

            _scoringEngineMock
                .Setup(e => e.EvaluateCandidates(procurementRequestId, request.CandidateVendors, null, null, null))
                .Returns(scoredResults);

            _repositoryMock
                .Setup(r => r.DeleteByProcurementRequestIdAsync(procurementRequestId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _repositoryMock
                .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<VendorEvaluationEntity>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _repositoryMock
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            // Act
            var summary = await _service.EvaluateAndRankCandidateVendorsAsync(request);

            // Assert: Cheaper vendor (Vendor 1) MUST be Rank #1 and TopRecommendedVendorId despite being second in the list
            Assert.Equal(vendor1Id, summary.TopRecommendedVendorId);
            Assert.Equal(1, summary.RankedEvaluations[0].Rank);
            Assert.Equal(vendor1Id, summary.RankedEvaluations[0].VendorId);
            Assert.Equal(2, summary.RankedEvaluations[1].Rank);
            Assert.Equal(vendor2Id, summary.RankedEvaluations[1].VendorId);
        }
    }
}
