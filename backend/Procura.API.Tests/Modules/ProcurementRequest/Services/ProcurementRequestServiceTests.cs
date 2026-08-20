using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Procura.API.Modules.ProcurementRequest.DTOs;
using ProcurementRequestEntity = Procura.API.Modules.ProcurementRequest.Entities.ProcurementRequest;
using ProcurementRequestItemEntity = Procura.API.Modules.ProcurementRequest.Entities.ProcurementRequestItem;
using Procura.API.Modules.ProcurementRequest.Enums;
using Procura.API.Modules.ProcurementRequest.Repositories;
using Procura.API.Modules.ProcurementRequest.Services;
using Xunit;

namespace Procura.API.Tests.Modules.ProcurementRequest.Services
{
    public class ProcurementRequestServiceTests
    {
        private readonly Mock<IProcurementRequestRepository> _repositoryMock;
        private readonly ProcurementRequestService _service;

        public ProcurementRequestServiceTests()
        {
            _repositoryMock = new Mock<IProcurementRequestRepository>();
            _service = new ProcurementRequestService(_repositoryMock.Object);
        }

        [Fact]
        public async Task CreateAsync_WithItems_CreatesDraftRequest()
        {
            // Arrange
            var requesterId = Guid.NewGuid();

            var dto = new CreateProcurementRequestDto
            {
                Title = "Office Supplies",
                Description = "Monthly office supplies",
                Justification = "Required for office operations",
                Priority = Priority.MEDIUM,
                RequiredByDate = DateTime.UtcNow.AddDays(7),
                Items = new List<CreateProcurementRequestItemDto>
                {
                    new CreateProcurementRequestItemDto
                    {
                        ItemName = "Printer Paper",
                        Description = "A4 paper",
                        Quantity = 10,
                        Unit = "Pack",
                        EstimatedUnitPrice = 1500
                    }
                }
            };

            _repositoryMock
                .Setup(r => r.GenerateUniqueRequestNumberAsync())
                .ReturnsAsync("PR-2026-00001");

            // Act
            var result = await _service.CreateAsync(dto, requesterId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PR-2026-00001", result.RequestNumber);
            Assert.Equal(requesterId, result.RequesterId);
            Assert.Equal("Office Supplies", result.Title);
            Assert.Equal("DRAFT", result.Status);
            Assert.Equal(15000, result.EstimatedTotal);
            Assert.Single(result.Items);

            _repositoryMock.Verify(
                r => r.AddAsync(It.IsAny<ProcurementRequestEntity>()),
                Times.Once);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task CreateAsync_WithoutItems_ThrowsArgumentException()
        {
            // Arrange
            var requesterId = Guid.NewGuid();

            var dto = new CreateProcurementRequestDto
            {
                Title = "Office Supplies",
                Description = "Test",
                Justification = "Test",
                Priority = Priority.MEDIUM,
                RequiredByDate = DateTime.UtcNow.AddDays(7),
                Items = new List<CreateProcurementRequestItemDto>()
            };

            // Act
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateAsync(dto, requesterId));

            // Assert
            Assert.Equal(
                "A procurement request must contain at least one item before submission.",
                exception.Message);

            _repositoryMock.Verify(
                r => r.AddAsync(It.IsAny<ProcurementRequestEntity>()),
                Times.Never);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_WhenRequestExists_ReturnsRequest()
        {
            // Arrange
            var requestId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = Guid.NewGuid(),
                RequestNumber = "PR-2026-00001",
                Title = "Laptop Purchase",
                Description = "Laptop",
                Justification = "Developer equipment",
                Priority = Priority.HIGH,
                RequiredByDate = DateTime.UtcNow.AddDays(10),
                Status = RequestStatus.DRAFT,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                EstimatedTotal = 250000,
                Items = new List<ProcurementRequestItemEntity>()
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act
            var result = await _service.GetByIdAsync(requestId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(requestId, result.Id);
            Assert.Equal("Laptop Purchase", result.Title);
            Assert.Equal("DRAFT", result.Status);
        }

        [Fact]
        public async Task GetByIdAsync_WhenRequestDoesNotExist_ReturnsNull()
        {
            // Arrange
            var requestId = Guid.NewGuid();

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync((ProcurementRequestEntity)null!);

            // Act
            var result = await _service.GetByIdAsync(requestId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SubmitAsync_WhenDraftWithItems_ChangesStatusToSubmitted()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = requesterId,
                RequestNumber = "PR-2026-00001",
                Title = "Office Equipment",
                Status = RequestStatus.DRAFT,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = new List<ProcurementRequestItemEntity>
                {
                    new ProcurementRequestItemEntity
                    {
                        ItemName = "Monitor",
                        Quantity = 2,
                        EstimatedUnitPrice = 50000,
                        Unit = "Unit"
                    }
                }
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act
            await _service.SubmitAsync(requestId, requesterId);

            // Assert
            Assert.Equal(RequestStatus.SUBMITTED, request.Status);

            _repositoryMock.Verify(
                r => r.Update(request),
                Times.Once);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task SubmitAsync_WhenUserIsNotRequester_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var actualRequesterId = Guid.NewGuid();
            var differentUserId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = actualRequesterId,
                Status = RequestStatus.DRAFT,
                Items = new List<ProcurementRequestItemEntity>
                {
                    new ProcurementRequestItemEntity
                    {
                        ItemName = "Monitor",
                        Quantity = 1,
                        EstimatedUnitPrice = 50000,
                        Unit = "Unit"
                    }
                }
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.SubmitAsync(requestId, differentUserId));

            // Assert
            Assert.Equal(
                "Not authorized to submit this request.",
                exception.Message);

            _repositoryMock.Verify(
                r => r.Update(It.IsAny<ProcurementRequestEntity>()),
                Times.Never);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Never);
        }

        [Fact]
        public async Task SubmitAsync_WhenRequestIsNotDraft_ThrowsInvalidOperationException()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = requesterId,
                Status = RequestStatus.SUBMITTED,
                Items = new List<ProcurementRequestItemEntity>
                {
                    new ProcurementRequestItemEntity
                    {
                        ItemName = "Monitor",
                        Quantity = 1,
                        EstimatedUnitPrice = 50000,
                        Unit = "Unit"
                    }
                }
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.SubmitAsync(requestId, requesterId));

            // Assert
            Assert.Equal(
                "Only DRAFT requests can be submitted.",
                exception.Message);
        }

        [Fact]
        public async Task UpdateStatusAsync_WithValidRoleAndTransition_ChangesStatus()
        {
            // Arrange
            var requestId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = Guid.NewGuid(),
                Status = RequestStatus.SUBMITTED,
                Items = new List<ProcurementRequestItemEntity>()
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act
            await _service.UpdateStatusAsync(
                requestId,
                RequestStatus.UNDER_EVALUATION,
                "PROCUREMENT_OFFICER");

            // Assert
            Assert.Equal(
                RequestStatus.UNDER_EVALUATION,
                request.Status);

            _repositoryMock.Verify(
                r => r.Update(request),
                Times.Once);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task UpdateStatusAsync_WithInvalidRoleAndTransition_ThrowsInvalidOperationException()
        {
            // Arrange
            var requestId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = Guid.NewGuid(),
                Status = RequestStatus.SUBMITTED,
                Items = new List<ProcurementRequestItemEntity>()
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.UpdateStatusAsync(
                    requestId,
                    RequestStatus.APPROVED,
                    "EMPLOYEE"));

            // Assert
            Assert.Contains(
                "Invalid state transition",
                exception.Message);

            Assert.Equal(
                RequestStatus.SUBMITTED,
                request.Status);

            _repositoryMock.Verify(
                r => r.Update(It.IsAny<ProcurementRequestEntity>()),
                Times.Never);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Never);
        }
    }
}