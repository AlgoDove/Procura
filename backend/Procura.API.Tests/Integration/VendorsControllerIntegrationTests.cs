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

        // VM-INT-05: Unauthenticated request to protected vendor endpoint returns 401 Unauthorized
        [Fact]
        public async Task GetVendors_WithoutToken_Returns401()
        {
            using var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = null;

            var response = await client.GetAsync("/api/vendors");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // VM-INT-06: EMPLOYEE is forbidden from updating a vendor (PUT requires PROCUREMENT_OFFICER or ADMIN)
        [Fact]
        public async Task UpdateVendor_AsEmployee_Returns403()
        {
            var token = await RegisterAndLoginAsync($"emp3-{Guid.NewGuid()}@test.com");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var updateBody = new
            {
                name = "Updated Vendor Name",
                contactPerson = "Updated Person",
                email = "updated@test.com",
                phoneNumber = "0770000000",
                category = "IT",
                rating = 4.0
            };

            var response = await _client.PutAsJsonAsync($"/api/vendors/{Guid.NewGuid()}", updateBody);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // VM-INT-07: PROCUREMENT_OFFICER can update an existing vendor
        [Fact]
        public async Task UpdateVendor_AsProcurementOfficer_UpdatesAndReturns200()
        {
            var token = await RegisterAndLoginAsync($"po3-{Guid.NewGuid()}@test.com", promoteToProcurementOfficer: true);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var createBody = new
            {
                name = "Original Vendor",
                contactPerson = "Orig Person",
                email = $"orig-{Guid.NewGuid()}@test.com",
                phoneNumber = "0771112233",
                category = "Office Supplies",
                rating = 3.5
            };
            var createRes = await _client.PostAsJsonAsync("/api/vendors", createBody);
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
            var createdJson = await createRes.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(createdJson);
            var vendorId = doc.RootElement.GetProperty("id").GetString();

            var updateBody = new
            {
                name = "Updated Vendor Name",
                contactPerson = "New Person",
                email = $"updated-{Guid.NewGuid()}@test.com",
                phoneNumber = "0779998877",
                category = "Office Supplies",
                rating = 4.8
            };
            var updateRes = await _client.PutAsJsonAsync($"/api/vendors/{vendorId}", updateBody);
            Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

            var updatedJson = await updateRes.Content.ReadAsStringAsync();
            using var updatedDoc = JsonDocument.Parse(updatedJson);
            Assert.Equal("Updated Vendor Name", updatedDoc.RootElement.GetProperty("name").GetString());
            Assert.Equal(4.8, updatedDoc.RootElement.GetProperty("rating").GetDouble());
        }

        // VM-INT-08: Invalid vendor payload fails validation and returns 400 BadRequest
        [Fact]
        public async Task CreateVendor_WithInvalidRating_Returns400()
        {
            var token = await RegisterAndLoginAsync($"po4-{Guid.NewGuid()}@test.com", promoteToProcurementOfficer: true);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var invalidBody = new
            {
                name = "Invalid Vendor",
                contactPerson = "Person",
                email = $"invalid-{Guid.NewGuid()}@test.com",
                phoneNumber = "0770000000",
                category = "Hardware",
                rating = 9.5 // Exceeds [Range(0.0, 5.0)]
            };

            var response = await _client.PostAsJsonAsync("/api/vendors", invalidBody);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // VM-INT-09: Swagger OpenAPI document is successfully generated and includes Bearer security scheme
        [Fact]
        public async Task SwaggerOpenApi_GeneratesDocument_WithBearerSecurityScheme()
        {
            using var client = _factory.CreateClient();
            var response = await client.GetAsync("/swagger/v1/swagger.json");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("paths", out var paths), "OpenAPI document must have paths.");
            Assert.True(paths.TryGetProperty("/api/vendors", out _), "OpenAPI must include /api/vendors path.");
            Assert.True(paths.TryGetProperty("/api/procurement-requests", out _), "OpenAPI must include /api/procurement-requests path.");

            // Verify Security Definitions / Components
            Assert.True(root.TryGetProperty("components", out var components), "OpenAPI document must have components.");
            Assert.True(components.TryGetProperty("securitySchemes", out var schemes), "OpenAPI document must have securitySchemes.");
            Assert.True(schemes.TryGetProperty("Bearer", out var bearerScheme), "Bearer security scheme must be configured.");
            Assert.Equal("bearer", bearerScheme.GetProperty("scheme").GetString());
        }
    }
}