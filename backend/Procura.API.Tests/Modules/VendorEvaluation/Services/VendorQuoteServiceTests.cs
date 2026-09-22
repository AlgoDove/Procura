using Moq;
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Entities;
using Procura.API.Modules.VendorEvaluation.Repositories;
using Procura.API.Modules.VendorEvaluation.Services;
using Procura.API.Modules.VendorManagement.DTOs;
using Procura.API.Modules.VendorManagement.Enums;
using Procura.API.Modules.VendorManagement.Services;
using Xunit;

namespace Procura.API.Tests.Modules.VendorEvaluation.Services;

public class VendorQuoteServiceTests
{
    private readonly Mock<IVendorQuoteRepository> _repositoryMock;
    private readonly Mock<IVendorService> _vendorServiceMock;
    private readonly VendorQuoteService _sut;

    public VendorQuoteServiceTests()
    {
        _repositoryMock = new Mock<IVendorQuoteRepository>();
        _vendorServiceMock = new Mock<IVendorService>();
        _sut = new VendorQuoteService(_repositoryMock.Object, _vendorServiceMock.Object);
    }

    private static VendorResponseDto MakeVendor(Guid id, VendorStatus status, string name = "Acme Supplies") => new()
    {
        Id = id,
        Name = name,
        ContactPerson = "Jane Doe",
        Email = "jane@acme.com",
        PhoneNumber = "0771234567",
        Category = "Hardware",
        Rating = 4.5m,
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static CreateVendorQuoteDto MakeCreateDto(Guid vendorId, Guid? requestId = null) => new(
        ProcurementRequestId: requestId ?? Guid.NewGuid(),
        VendorId: vendorId,
        VendorName: "Acme Supplies",
        QuotedPrice: 1500.00m,
        EstimatedDeliveryDays: 7,
        ReliabilityRating: 4.2m,
        IsComplianceApproved: true,
        Notes: "Bulk discount applied"
    );

    // ---------- SubmitQuoteAsync ----------

    [Fact]
    public async Task SubmitQuoteAsync_VendorExistsAndActive_AddsQuoteAndReturnsMappedDto()
    {
        var vendorId = Guid.NewGuid();
        var vendor = MakeVendor(vendorId, VendorStatus.ACTIVE);
        var dto = MakeCreateDto(vendorId);

        _vendorServiceMock.Setup(v => v.GetByIdAsync(vendorId)).ReturnsAsync(vendor);

        VendorQuote? capturedQuote = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<VendorQuote>(), It.IsAny<CancellationToken>()))
            .Callback<VendorQuote, CancellationToken>((q, _) => capturedQuote = q)
            .ReturnsAsync((VendorQuote q, CancellationToken _) => q);
        _repositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.SubmitQuoteAsync(dto);

        Assert.NotNull(capturedQuote);
        Assert.Equal(dto.VendorId, capturedQuote!.VendorId);
        Assert.Equal(dto.ProcurementRequestId, capturedQuote.ProcurementRequestId);
        Assert.Equal(dto.QuotedPrice, capturedQuote.QuotedPrice);

        Assert.Equal(capturedQuote.Id, result.Id);
        Assert.Equal(dto.VendorName, result.VendorName);
        Assert.Equal(dto.QuotedPrice, result.QuotedPrice);
        Assert.Equal(dto.EstimatedDeliveryDays, result.EstimatedDeliveryDays);
        Assert.Equal(dto.ReliabilityRating, result.ReliabilityRating);
        Assert.Equal(dto.IsComplianceApproved, result.IsComplianceApproved);
        Assert.Equal(dto.Notes, result.Notes);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<VendorQuote>(), It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitQuoteAsync_VendorDoesNotExist_ThrowsInvalidOperationException_AndDoesNotSave()
    {
        var vendorId = Guid.NewGuid();
        var dto = MakeCreateDto(vendorId);

        _vendorServiceMock.Setup(v => v.GetByIdAsync(vendorId)).ReturnsAsync((VendorResponseDto?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SubmitQuoteAsync(dto));

        Assert.Contains(vendorId.ToString(), ex.Message);
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<VendorQuote>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitQuoteAsync_VendorInactive_ThrowsInvalidOperationException_AndDoesNotSave()
    {
        var vendorId = Guid.NewGuid();
        var vendor = MakeVendor(vendorId, VendorStatus.INACTIVE, name: "Deactivated Co");
        var dto = MakeCreateDto(vendorId);

        _vendorServiceMock.Setup(v => v.GetByIdAsync(vendorId)).ReturnsAsync(vendor);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SubmitQuoteAsync(dto));

        Assert.Contains("Deactivated Co", ex.Message);
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<VendorQuote>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- GetQuotesByRequestIdAsync ----------

    [Fact]
    public async Task GetQuotesByRequestIdAsync_ReturnsMappedDtosForMatchingRequest()
    {
        var requestId = Guid.NewGuid();
        var quotes = new List<VendorQuote>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = requestId,
                VendorId = Guid.NewGuid(),
                VendorName = "Vendor A",
                QuotedPrice = 1000m,
                EstimatedDeliveryDays = 5,
                ReliabilityRating = 4.0m,
                IsComplianceApproved = true,
                Notes = null,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = requestId,
                VendorId = Guid.NewGuid(),
                VendorName = "Vendor B",
                QuotedPrice = 1200m,
                EstimatedDeliveryDays = 3,
                ReliabilityRating = 4.8m,
                IsComplianceApproved = false,
                Notes = "Pending compliance review",
                CreatedAt = DateTime.UtcNow
            }
        };

        _repositoryMock
            .Setup(r => r.GetByProcurementRequestIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotes);

        var result = await _sut.GetQuotesByRequestIdAsync(requestId);

        Assert.Equal(2, result.Count);
        Assert.Equal(quotes[0].Id, result[0].Id);
        Assert.Equal(quotes[0].VendorName, result[0].VendorName);
        Assert.Equal(quotes[1].Id, result[1].Id);
        Assert.Equal(quotes[1].Notes, result[1].Notes);
    }

    [Fact]
    public async Task GetQuotesByRequestIdAsync_NoQuotesFound_ReturnsEmptyList()
    {
        var requestId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByProcurementRequestIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VendorQuote>());

        var result = await _sut.GetQuotesByRequestIdAsync(requestId);

        Assert.Empty(result);
    }

    // ---------- GetQuoteByIdAsync ----------

    [Fact]
    public async Task GetQuoteByIdAsync_QuoteExists_ReturnsMappedDto()
    {
        var quote = new VendorQuote
        {
            Id = Guid.NewGuid(),
            ProcurementRequestId = Guid.NewGuid(),
            VendorId = Guid.NewGuid(),
            VendorName = "Vendor C",
            QuotedPrice = 999.99m,
            EstimatedDeliveryDays = 10,
            ReliabilityRating = 3.9m,
            IsComplianceApproved = true,
            Notes = "Rush order",
            CreatedAt = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(quote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var result = await _sut.GetQuoteByIdAsync(quote.Id);

        Assert.NotNull(result);
        Assert.Equal(quote.Id, result!.Id);
        Assert.Equal(quote.VendorName, result.VendorName);
        Assert.Equal(quote.QuotedPrice, result.QuotedPrice);
        Assert.Equal(quote.Notes, result.Notes);
    }

    [Fact]
    public async Task GetQuoteByIdAsync_QuoteDoesNotExist_ReturnsNull()
    {
        var id = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorQuote?)null);

        var result = await _sut.GetQuoteByIdAsync(id);

        Assert.Null(result);
    }
}