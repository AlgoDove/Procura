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
    public class VendorsControllerIntegrationTests : IClassFixture<VendorManagementWebApplicationFactory>
    {
        private readonly VendorManagementWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public VendorsControllerIntegrationTests(VendorManagementWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<string> RegisterAndLoginAsync(string email, bool promoteToProcurementOfficer = false)
        {
            // Real registration through the real endpoint - always creates EMPLOYEE.
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
                // Known gap: register can't set a role. Patch it directly in the test database,
                // mirroring the manual workaround used in Postman during manual testing.
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

        // VM-INT-01: EMPLOYEE is correctly blocked from creating a vendor (proves real [Authorize(Roles=...)] works)
        [Fact]
        public async Task CreateVendor_AsEmployee_Returns403()
        {
            var token = await RegisterAndLoginAsync($"emp-{Guid.NewGuid()}@test.com");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var body = new
            {
                name = "Test Vendor",
                contactPerson = "X",
                email = $"vendor-{Guid.NewGuid()}@test.com",
                phoneNumber = "0000000000",
                category = "Office Supplies",
                rating = 3
            };
            var response = await _client.PostAsJsonAsync("/api/vendors", body);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // VM-INT-02: PROCUREMENT_OFFICER can create a vendor through the real, authenticated pipeline
        [Fact]
        public async Task CreateVendor_AsProcurementOfficer_Returns201()
        {
            var token = await RegisterAndLoginAsync($"po-{Guid.NewGuid()}@test.com", promoteToProcurementOfficer: true);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var body = new
            {
                name = "Acme Office Supplies",
                contactPerson = "John Silva",
                email = $"vendor-{Guid.NewGuid()}@test.com",
                phoneNumber = "0771234567",
                category = "Office Supplies",
                rating = 4.5
            };
            var response = await _client.PostAsJsonAsync("/api/vendors", body);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        // VM-INT-03: any authenticated user (including EMPLOYEE) can read the vendor list
        [Fact]
        public async Task GetAllVendors_AsEmployee_Returns200()
        {
            var token = await RegisterAndLoginAsync($"emp2-{Guid.NewGuid()}@test.com");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/vendors");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // VM-INT-04: deactivating an already-inactive vendor returns 409 through the real HTTP pipeline
        [Fact]
        public async Task DeactivateVendor_Twice_SecondCallReturns409()
        {
            var token = await RegisterAndLoginAsync($"po2-{Guid.NewGuid()}@test.com", promoteToProcurementOfficer: true);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var createBody = new
            {
                name = "Deactivate Test Vendor",
                contactPerson = "X",
                email = $"vendor-{Guid.NewGuid()}@test.com",
                phoneNumber = "0000000000",
                category = "Office Supplies",
                rating = 3
            };
            var createResponse = await _client.PostAsJsonAsync("/api/vendors", createBody);
            var createdJson = await createResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(createdJson);
            var vendorId = doc.RootElement.GetProperty("id").GetString();

            var firstDeactivate = await _client.PostAsync($"/api/vendors/{vendorId}/deactivate", null);
            Assert.Equal(HttpStatusCode.OK, firstDeactivate.StatusCode);

            var secondDeactivate = await _client.PostAsync($"/api/vendors/{vendorId}/deactivate", null);
            Assert.Equal(HttpStatusCode.Conflict, secondDeactivate.StatusCode);
        }
    }
}