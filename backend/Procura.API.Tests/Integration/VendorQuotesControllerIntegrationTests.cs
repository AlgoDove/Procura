using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procura.API.Shared.Data;
using Procura.API.Shared.Enums;
using Xunit;

namespace Procura.API.Tests.Integration
{
    public class VendorQuotesControllerIntegrationTests : IClassFixture<VendorManagementWebApplicationFactory>
    {
        private readonly VendorManagementWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public VendorQuotesControllerIntegrationTests(VendorManagementWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<string> RegisterAndLoginAsync(string email, bool promoteToProcurementOfficer = false)
        {
            var registerBody = new
            {
                firstName = "Test",
                lastName = "User",
                email,
                password = "Test1234!"
            };
            var registerResponse = await _client.PostAsJsonAsync("/api/Auth/register", registerBody);
            Assert.True(registerResponse.IsSuccessStatusCode, $"Register failed: {await registerResponse.Content.ReadAsStringAsync()}");

            if (promoteToProcurementOfficer)
            {
                using var scope = _factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.FirstAsync(u => u.Email == email);
                user.Role = SystemRole.PROCUREMENT_OFFICER;
                await db.SaveChangesAsync();
            }

            var loginBody = new { email, password = "Test1234!" };
            var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", loginBody);
            Assert.True(loginResponse.IsSuccessStatusCode, $"Login failed: {await loginResponse.Content.ReadAsStringAsync()}");

            var loginJson = await loginResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(loginJson);
            return doc.RootElement.GetProperty("token").GetString()!;
        }

        private async Task<string> CreateActiveVendorAsync(string procurementOfficerToken)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", procurementOfficerToken);

            var body = new
            {
                name = "Quote Test Vendor",
                contactPerson = "X",
                email = $"vendor-{Guid.NewGuid()}@test.com",
                phoneNumber = "0000000000",
                category = "Office Supplies",
                rating = 4
            };
            var response = await _client.PostAsJsonAsync("/api/vendors", body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("id").GetString()!;
        }

        // VQ-INT-01: EMPLOYEE is blocked from submitting a quote entirely (proves real [Authorize(Roles=...)] works)
        [Fact]
        public async Task SubmitQuote_AsEmployee_Returns403()
        {
            var token = await RegisterAndLoginAsync($"emp-{Guid.NewGuid()}@test.com");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var body = new
            {
                procurementRequestId = Guid.NewGuid(),
                vendorId = Guid.NewGuid(),
                vendorName = "Irrelevant Vendor",
                quotedPrice = 1000,
                estimatedDeliveryDays = 5,
                reliabilityRating = 80,
                isComplianceApproved = true,
                notes = (string?)null
            };
            var response = await _client.PostAsJsonAsync("/api/vendor-quotes", body);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // VQ-INT-02: unauthenticated request is rejected with 401, not 403 (proves the 401 vs 403 distinction actually works)
        [Fact]
        public async Task SubmitQuote_Unauthenticated_Returns401()
        {
            var freshClient = _factory.CreateClient(); // no Authorization header attached at all

            var body = new
            {
                procurementRequestId = Guid.NewGuid(),
                vendorId = Guid.NewGuid(),
                vendorName = "Irrelevant Vendor",
                quotedPrice = 1000,
                estimatedDeliveryDays = 5,
                reliabilityRating = 80,
                isComplianceApproved = true,
                notes = (string?)null
            };
            var response = await freshClient.PostAsJsonAsync("/api/vendor-quotes", body);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // VQ-INT-03: PROCUREMENT_OFFICER can submit a quote for a real, active vendor through the full real pipeline
        [Fact]
        public async Task SubmitQuote_AsProcurementOfficer_WithActiveVendor_Returns201()
        {
            var poToken = await RegisterAndLoginAsync($"po-{Guid.NewGuid()}@test.com", promoteToProcurementOfficer: true);
            var vendorId = await CreateActiveVendorAsync(poToken);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", poToken);

            var body = new
            {
                procurementRequestId = Guid.NewGuid(),
                vendorId = Guid.Parse(vendorId),
                vendorName = "Quote Test Vendor",
                quotedPrice = 18500.00,
                estimatedDeliveryDays = 12,
                reliabilityRating = 88,
                isComplianceApproved = true,
                notes = "Integration test quote"
            };
            var response = await _client.PostAsJsonAsync("/api/vendor-quotes", body);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        // VQ-INT-04: submitting a quote for a VendorId that doesn't exist in Vendor Management is rejected
        // through the real pipeline (proves the cross-module integration check actually runs end-to-end, not just in the unit test mock)
        [Fact]
        public async Task SubmitQuote_WithNonexistentVendor_ReturnsConflict()
        {
            var poToken = await RegisterAndLoginAsync($"po2-{Guid.NewGuid()}@test.com", promoteToProcurementOfficer: true);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", poToken);

            var body = new
            {
                procurementRequestId = Guid.NewGuid(),
                vendorId = Guid.NewGuid(), // never created — does not exist
                vendorName = "Ghost Vendor",
                quotedPrice = 1000,
                estimatedDeliveryDays = 5,
                reliabilityRating = 80,
                isComplianceApproved = true,
                notes = (string?)null
            };
            var response = await _client.PostAsJsonAsync("/api/vendor-quotes", body);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        // VQ-INT-05: EMPLOYEE is blocked from even viewing quotes (this controller has no read-only carve-out, unlike VendorsController)
        [Fact]
        public async Task GetQuotesByRequestId_AsEmployee_Returns403()
        {
            var token = await RegisterAndLoginAsync($"emp2-{Guid.NewGuid()}@test.com");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync($"/api/vendor-quotes/procurement-request/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // VQ-INT-06: fetching a quote that doesn't exist returns 404 through the real pipeline
        [Fact]
        public async Task GetQuoteById_WhenNotFound_Returns404()
        {
            var poToken = await RegisterAndLoginAsync($"po3-{Guid.NewGuid()}@test.com", promoteToProcurementOfficer: true);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", poToken);

            var response = await _client.GetAsync($"/api/vendor-quotes/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    

        private async Task<string> CreateInactiveVendorAsync(string procurementOfficerToken)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", procurementOfficerToken);

            var body = new
            {
                name = "Inactive Quote Test Vendor",
                contactPerson = "X",
                email = $"vendor-{Guid.NewGuid()}@test.com",
                phoneNumber = "0000000000",
                category = "Office Supplies",
                rating = 4
            };
            var createResponse = await _client.PostAsJsonAsync("/api/vendors", body);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var json = await createResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var vendorId = doc.RootElement.GetProperty("id").GetString()!;

            var deactivateResponse = await _client.PostAsync($"/api/vendors/{vendorId}/deactivate", null);
            Assert.True(deactivateResponse.IsSuccessStatusCode,
                $"Deactivate failed: {await deactivateResponse.Content.ReadAsStringAsync()}");

            return vendorId;
        }

        // VQ-INT-07: submitting a quote for a real but INACTIVE vendor is rejected through the real pipeline
        // (proves the inactive-vendor business rule maps to the correct HTTP status end-to-end, not just in the unit test mock)
        [Fact]
        public async Task SubmitQuote_WithInactiveVendor_ReturnsConflict()
        {
            var poToken = await RegisterAndLoginAsync($"po4-{Guid.NewGuid()}@test.com", promoteToProcurementOfficer: true);
            var vendorId = await CreateInactiveVendorAsync(poToken);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", poToken);

            var body = new
            {
                procurementRequestId = Guid.NewGuid(),
                vendorId = Guid.Parse(vendorId),
                vendorName = "Inactive Quote Test Vendor",
                quotedPrice = 1000,
                estimatedDeliveryDays = 5,
                reliabilityRating = 80,
                isComplianceApproved = true,
                notes = (string?)null
            };
            var response = await _client.PostAsJsonAsync("/api/vendor-quotes", body);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        // VQ-INT-08: PROCUREMENT_OFFICER can fetch a real, previously-submitted quote and gets back correct data
        // (proves the successful GET path returns real data, not just that error paths return the right codes)
        [Fact]
        public async Task GetQuoteById_WhenFound_ReturnsCorrectData()
        {
            var poToken = await RegisterAndLoginAsync($"po5-{Guid.NewGuid()}@test.com", promoteToProcurementOfficer: true);
            var vendorId = await CreateActiveVendorAsync(poToken);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", poToken);

            var procurementRequestId = Guid.NewGuid();
            var submitBody = new
            {
                procurementRequestId,
                vendorId = Guid.Parse(vendorId),
                vendorName = "Quote Test Vendor",
                quotedPrice = 22750.50,
                estimatedDeliveryDays = 9,
                reliabilityRating = 91,
                isComplianceApproved = true,
                notes = "Fetch-back test quote"
            };
            var submitResponse = await _client.PostAsJsonAsync("/api/vendor-quotes", submitBody);
            Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);

            var submitJson = await submitResponse.Content.ReadAsStringAsync();
            using var submitDoc = JsonDocument.Parse(submitJson);
            var quoteId = submitDoc.RootElement.GetProperty("id").GetString()!;

            var getResponse = await _client.GetAsync($"/api/vendor-quotes/{quoteId}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var getJson = await getResponse.Content.ReadAsStringAsync();
            using var getDoc = JsonDocument.Parse(getJson);
            var root = getDoc.RootElement;

            Assert.Equal(quoteId, root.GetProperty("id").GetString());
            Assert.Equal(procurementRequestId, root.GetProperty("procurementRequestId").GetGuid());
            Assert.Equal("Quote Test Vendor", root.GetProperty("vendorName").GetString());
            Assert.Equal(22750.50m, root.GetProperty("quotedPrice").GetDecimal());
            Assert.Equal(9, root.GetProperty("estimatedDeliveryDays").GetInt32());
            Assert.Equal("Fetch-back test quote", root.GetProperty("notes").GetString());
        }
    }
}