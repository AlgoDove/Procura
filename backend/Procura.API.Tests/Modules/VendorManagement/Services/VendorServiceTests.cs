using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Procura.API.Modules.VendorManagement.DTOs;
using Procura.API.Modules.VendorManagement.Entities;
using Procura.API.Modules.VendorManagement.Enums;
using Procura.API.Modules.VendorManagement.Repositories;
using Procura.API.Modules.VendorManagement.Services;
using Xunit;

namespace Procura.API.Tests.Modules.VendorManagement.Services
{
    public class VendorServiceTests
    {
        private readonly Mock<IVendorRepository> _repositoryMock;
        private readonly VendorService _service;

        public VendorServiceTests()
        {
            _repositoryMock = new Mock<IVendorRepository>();
            _service = new VendorService(_repositoryMock.Object);
        }

        // VM-UT-01: creating a vendor with valid data succeeds
        [Fact]
        public async Task CreateAsync_WithValidData_CreatesVendor()
        {
            // Arrange
            var dto = new CreateVendorDto
            {
                Name = "Acme Office Supplies",
                ContactPerson = "John Silva",
                Email = "john@acme.com",
                PhoneNumber = "0771234567",
                Category = "Office Supplies",
                Rating = 4.5m
            };

            _repositoryMock.Setup(r => r.EmailExistsAsync(dto.Email)).ReturnsAsync(false);
            _repositoryMock
                .Setup(r => r.AddAsync(It.IsAny<Vendor>()))
                .ReturnsAsync((Vendor v) => v);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Acme Office Supplies", result.Name);
            Assert.Equal(VendorStatus.ACTIVE, result.Status);
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Vendor>()), Times.Once);
        }

        // VM-UT-02: rating above 5 (invalid/boundary case) is rejected
        [Fact]
        public async Task CreateAsync_WithRatingAboveFive_ThrowsArgumentException()
        {
            var dto = new CreateVendorDto
            {
                Name = "Bad Vendor",
                ContactPerson = "X",
                Email = "x@x.com",
                PhoneNumber = "000",
                Category = "Other",
                Rating = 5.5m
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        // VM-UT-03: duplicate email is rejected
        [Fact]
        public async Task CreateAsync_WithDuplicateEmail_ThrowsInvalidOperationException()
        {
            var dto = new CreateVendorDto
            {
                Name = "Duplicate Co",
                ContactPerson = "X",
                Email = "existing@acme.com",
                PhoneNumber = "000",
                Category = "Other",
                Rating = 3
            };

            _repositoryMock.Setup(r => r.EmailExistsAsync(dto.Email)).ReturnsAsync(true);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(dto));
        }

        // VM-UT-04: fetching a vendor that doesn't exist returns null (controller maps this to 404)
        [Fact]
        public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
        {
            var id = Guid.NewGuid();
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Vendor?)null);

            var result = await _service.GetByIdAsync(id);

            Assert.Null(result);
        }

        // VM-UT-05: deactivating an already-inactive vendor is rejected (matches the 409 you proved in Postman)
        [Fact]
        public async Task DeactivateAsync_WhenAlreadyInactive_ThrowsInvalidOperationException()
        {
            var id = Guid.NewGuid();
            var vendor = new Vendor { Id = id, Status = VendorStatus.INACTIVE };
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(vendor);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeactivateAsync(id));
        }

        // VM-UT-06: deactivating an active vendor succeeds and flips its status
        [Fact]
        public async Task DeactivateAsync_WhenActive_SetsStatusToInactive()
        {
            var id = Guid.NewGuid();
            var vendor = new Vendor { Id = id, Status = VendorStatus.ACTIVE };
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(vendor);
            _repositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Vendor>()))
                .ReturnsAsync((Vendor v) => v);

            var result = await _service.DeactivateAsync(id);

            Assert.Equal(VendorStatus.INACTIVE, result.Status);
        }
    }
}