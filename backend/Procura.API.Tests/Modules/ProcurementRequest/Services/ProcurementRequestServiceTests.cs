using System;
using System.Collections.Generic;
using System.Linq;
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

        [Fact]
        public async Task UpdateAsync_WhenDraftAndRequester_SuccessfullyUpdatesPropertiesAndRecalculatesTotal()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = requesterId,
                RequestNumber = "PR-2026-00001",
                Title = "Old Title",
                Description = "Old Description",
                Justification = "Old Justification",
                Priority = Priority.LOW,
                RequiredByDate = DateTime.UtcNow.AddDays(5),
                Status = RequestStatus.DRAFT,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                EstimatedTotal = 500,
                Items = new List<ProcurementRequestItemEntity>
                {
                    new ProcurementRequestItemEntity
                    {
                        Id = Guid.NewGuid(),
                        ItemName = "Old Item",
                        Description = "Old Item Desc",
                        Quantity = 1,
                        Unit = "Unit",
                        EstimatedUnitPrice = 500
                    }
                }
            };

            var dto = new UpdateProcurementRequestDto
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Justification = "Updated Justification",
                Priority = Priority.HIGH,
                RequiredByDate = DateTime.UtcNow.AddDays(15),
                Items = new List<CreateProcurementRequestItemDto>
                {
                    new CreateProcurementRequestItemDto
                    {
                        ItemName = "New Item 1",
                        Description = "Item 1 Desc",
                        Quantity = 2,
                        Unit = "Box",
                        EstimatedUnitPrice = 1000
                    },
                    new CreateProcurementRequestItemDto
                    {
                        ItemName = "New Item 2",
                        Description = "Item 2 Desc",
                        Quantity = 3,
                        Unit = "Pack",
                        EstimatedUnitPrice = 500
                    }
                }
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act
            var result = await _service.UpdateAsync(requestId, dto, requesterId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated Title", result.Title);
            Assert.Equal("Updated Description", result.Description);
            Assert.Equal("Updated Justification", result.Justification);
            Assert.Equal("HIGH", result.Priority);
            Assert.Equal(3500, result.EstimatedTotal);
            Assert.Equal(2, result.Items.Count);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenUserIsNotRequester_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var actualRequesterId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = actualRequesterId,
                Status = RequestStatus.DRAFT,
                Items = new List<ProcurementRequestItemEntity>()
            };

            var dto = new UpdateProcurementRequestDto
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Justification = "Updated Justification",
                Priority = Priority.HIGH,
                RequiredByDate = DateTime.UtcNow.AddDays(10),
                Items = new List<CreateProcurementRequestItemDto>
                {
                    new CreateProcurementRequestItemDto
                    {
                        ItemName = "Item",
                        Description = "Desc",
                        Quantity = 1,
                        Unit = "Unit",
                        EstimatedUnitPrice = 100
                    }
                }
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.UpdateAsync(requestId, dto, unauthorizedUserId));

            Assert.Equal("Not authorized to update this request.", exception.Message);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenRequestIsNotDraft_ThrowsInvalidOperationException()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = requesterId,
                Status = RequestStatus.SUBMITTED,
                Items = new List<ProcurementRequestItemEntity>()
            };

            var dto = new UpdateProcurementRequestDto
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Justification = "Updated Justification",
                Priority = Priority.HIGH,
                RequiredByDate = DateTime.UtcNow.AddDays(10),
                Items = new List<CreateProcurementRequestItemDto>
                {
                    new CreateProcurementRequestItemDto
                    {
                        ItemName = "Item",
                        Description = "Desc",
                        Quantity = 1,
                        Unit = "Unit",
                        EstimatedUnitPrice = 100
                    }
                }
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.UpdateAsync(requestId, dto, requesterId));

            Assert.Equal("Only DRAFT requests can be modified.", exception.Message);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenItemsEmpty_ThrowsArgumentException()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = requesterId,
                Status = RequestStatus.DRAFT,
                Items = new List<ProcurementRequestItemEntity>()
            };

            var dto = new UpdateProcurementRequestDto
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Justification = "Updated Justification",
                Priority = Priority.HIGH,
                RequiredByDate = DateTime.UtcNow.AddDays(10),
                Items = new List<CreateProcurementRequestItemDto>()
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.UpdateAsync(requestId, dto, requesterId));

            Assert.Equal("A procurement request must contain at least one item.", exception.Message);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenRequestDoesNotExist_ThrowsKeyNotFoundException()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var dto = new UpdateProcurementRequestDto
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Justification = "Updated Justification",
                Priority = Priority.HIGH,
                RequiredByDate = DateTime.UtcNow.AddDays(10),
                Items = new List<CreateProcurementRequestItemDto>
                {
                    new CreateProcurementRequestItemDto
                    {
                        ItemName = "Item",
                        Description = "Desc",
                        Quantity = 1,
                        Unit = "Unit",
                        EstimatedUnitPrice = 100
                    }
                }
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync((ProcurementRequestEntity)null!);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _service.UpdateAsync(requestId, dto, requesterId));

            Assert.Equal("Request not found.", exception.Message);
        }

        [Fact]
        public async Task DeleteAsync_WhenDraftAndRequester_SuccessfullyDeletes()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = requesterId,
                Status = RequestStatus.DRAFT
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act
            await _service.DeleteAsync(requestId, requesterId);

            // Assert
            _repositoryMock.Verify(
                r => r.Remove(request),
                Times.Once);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WhenUserIsNotRequester_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var actualRequesterId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = actualRequesterId,
                Status = RequestStatus.DRAFT
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.DeleteAsync(requestId, unauthorizedUserId));

            Assert.Equal("Not authorized to delete this request.", exception.Message);

            _repositoryMock.Verify(
                r => r.Remove(It.IsAny<ProcurementRequestEntity>()),
                Times.Never);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WhenRequestIsNotDraft_ThrowsInvalidOperationException()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = requesterId,
                Status = RequestStatus.APPROVED
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.DeleteAsync(requestId, requesterId));

            Assert.Equal("Only DRAFT requests can be deleted.", exception.Message);

            _repositoryMock.Verify(
                r => r.Remove(It.IsAny<ProcurementRequestEntity>()),
                Times.Never);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WhenRequestDoesNotExist_ThrowsKeyNotFoundException()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync((ProcurementRequestEntity)null!);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _service.DeleteAsync(requestId, requesterId));

            Assert.Equal("Request not found.", exception.Message);
        }

        [Fact]
        public async Task GetRequestsAsync_WhenRoleIsEmployee_ReturnsOnlyUserRequests()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var userRequests = new List<ProcurementRequestEntity>
            {
                new ProcurementRequestEntity
                {
                    Id = Guid.NewGuid(),
                    RequesterId = userId,
                    RequestNumber = "PR-2026-00001",
                    Title = "User Request 1",
                    Status = RequestStatus.DRAFT,
                    Items = new List<ProcurementRequestItemEntity>()
                },
                new ProcurementRequestEntity
                {
                    Id = Guid.NewGuid(),
                    RequesterId = userId,
                    RequestNumber = "PR-2026-00002",
                    Title = "User Request 2",
                    Status = RequestStatus.SUBMITTED,
                    Items = new List<ProcurementRequestItemEntity>()
                }
            };

            _repositoryMock
                .Setup(r => r.GetAllByUserIdAsync(userId))
                .ReturnsAsync(userRequests);

            // Act
            var results = await _service.GetRequestsAsync(userId, "EMPLOYEE");

            // Assert
            Assert.NotNull(results);
            Assert.Equal(2, results.Count());

            _repositoryMock.Verify(
                r => r.GetAllByUserIdAsync(userId),
                Times.Once);

            _repositoryMock.Verify(
                r => r.GetAllAsync(),
                Times.Never);
        }

        [Theory]
        [InlineData("MANAGER")]
        [InlineData("ADMIN")]
        [InlineData("PROCUREMENT_OFFICER")]
        public async Task GetRequestsAsync_WhenRoleIsPrivileged_ReturnsAllRequests(string role)
        {
            // Arrange
            var userId = Guid.NewGuid();

            var allRequests = new List<ProcurementRequestEntity>
            {
                new ProcurementRequestEntity
                {
                    Id = Guid.NewGuid(),
                    RequesterId = Guid.NewGuid(),
                    RequestNumber = "PR-2026-00001",
                    Title = "Request 1",
                    Status = RequestStatus.DRAFT,
                    Items = new List<ProcurementRequestItemEntity>()
                },
                new ProcurementRequestEntity
                {
                    Id = Guid.NewGuid(),
                    RequesterId = Guid.NewGuid(),
                    RequestNumber = "PR-2026-00002",
                    Title = "Request 2",
                    Status = RequestStatus.SUBMITTED,
                    Items = new List<ProcurementRequestItemEntity>()
                },
                new ProcurementRequestEntity
                {
                    Id = Guid.NewGuid(),
                    RequesterId = Guid.NewGuid(),
                    RequestNumber = "PR-2026-00003",
                    Title = "Request 3",
                    Status = RequestStatus.APPROVED,
                    Items = new List<ProcurementRequestItemEntity>()
                }
            };

            _repositoryMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(allRequests);

            // Act
            var results = await _service.GetRequestsAsync(userId, role);

            // Assert
            Assert.NotNull(results);
            Assert.Equal(3, results.Count());

            _repositoryMock.Verify(
                r => r.GetAllAsync(),
                Times.Once);

            _repositoryMock.Verify(
                r => r.GetAllByUserIdAsync(It.IsAny<Guid>()),
                Times.Never);
        }

        [Fact]
        public async Task SubmitAsync_WhenDraftHasNoItems_ThrowsInvalidOperationException()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = requesterId,
                Status = RequestStatus.DRAFT,
                Items = new List<ProcurementRequestItemEntity>()
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.SubmitAsync(requestId, requesterId));

            Assert.Equal("A procurement request must contain at least one item before submission.", exception.Message);

            _repositoryMock.Verify(
                r => r.Update(It.IsAny<ProcurementRequestEntity>()),
                Times.Never);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WhenAdmin_CanDeleteOtherUserDraftRequest()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var request = new ProcurementRequestEntity
            {
                Id = requestId,
                RequesterId = requesterId,
                Status = RequestStatus.DRAFT
            };

            _repositoryMock
                .Setup(r => r.GetByIdAsync(requestId))
                .ReturnsAsync(request);

            // Act
            await _service.DeleteAsync(requestId, adminId, "ADMIN");

            // Assert
            _repositoryMock.Verify(
                r => r.Remove(request),
                Times.Once);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(),
                Times.Once);
        }
    }
}