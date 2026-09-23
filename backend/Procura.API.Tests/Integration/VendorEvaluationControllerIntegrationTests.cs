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
    public class VendorEvaluationControllerIntegrationTests : IClassFixture<VendorManagementWebApplicationFactory>
    {
        private readonly VendorManagementWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public VendorEvaluationControllerIntegrationTests(VendorManagementWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<string> RegisterAndLoginAsync(string email, SystemRole? promoteTo = null)
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

            if (promoteTo.HasValue)
            {
                using var scope = _factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.FirstAsync(u => u.Email == email);
                user.Role = promoteTo.Value;
                await db.SaveChangesAsync();
            }

            var loginBody = new { email, password = "Test1234!" };
            var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", loginBody);
            Assert.True(loginResponse.IsSuccessStatusCode, $"Login failed: {await loginResponse.Content.ReadAsStringAsync()}");

            var loginJson = await loginResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(loginJson);
            return doc.RootElement.GetProperty("token").GetString()!;
        }

        private async Task<string> CreateEvaluationAsync(string token)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var body = new
            {
                procurementRequestId = Guid.NewGuid(),
                vendorId = Guid.NewGuid(),
                rank = 1,
                overallScore = 87.5,
                reasoning = "Integration test evaluation",
                riskFlags = new string[] { },
                generatedByAgent = false,
                criterionScores = new object[]
                {
                    new { criterionName = "PRICE", score = 90, weight = 0.35 }
                }
            };
            var response = await _client.PostAsJsonAsync("/api/vendor-evaluations", body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("id").GetString()!;
        }

        // VE-INT-01: EMPLOYEE is blocked entirely from the evaluations controller
        [Fact]
        public async Task Evaluate_AsEmployee_Returns403()
        {
            var token = await RegisterAndLoginAsync($"emp-{Guid.NewGuid()}@test.com");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PostAsJsonAsync("/api/vendor-evaluations/evaluate",
                new { procurementRequestId = Guid.NewGuid() });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // VE-INT-02: unauthenticated request returns 401, not 403
        [Fact]
        public async Task GetById_Unauthenticated_Returns401()
        {
            var freshClient = _factory.CreateClient();

            var response = await freshClient.GetAsync($"/api/vendor-evaluations/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // VE-INT-03: ADMIN can view evaluations — proves the ADMIN role fix actually works end-to-end, not just in the attribute
        [Fact]
        public async Task GetByProcurementRequestId_AsAdmin_Returns200()
        {
            var token = await RegisterAndLoginAsync($"admin-{Guid.NewGuid()}@test.com", SystemRole.ADMIN);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync($"/api/vendor-evaluations/procurement-request/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // VE-INT-04: PROCUREMENT_OFFICER can create a manual evaluation record through the real pipeline
        [Fact]
        public async Task CreateEvaluation_AsProcurementOfficer_Returns201()
        {
            var token = await RegisterAndLoginAsync($"po-{Guid.NewGuid()}@test.com", SystemRole.PROCUREMENT_OFFICER);
            var id = await CreateEvaluationAsync(token);

            Assert.False(string.IsNullOrEmpty(id));
        }

        // VE-INT-05: PROCUREMENT_OFFICER can do everything else but is correctly blocked from DELETE specifically,
        // proving the class-level Authorize + method-level Authorize combine as AND, not OR
        [Fact]
        public async Task DeleteEvaluation_AsProcurementOfficer_Returns403()
        {
            var poToken = await RegisterAndLoginAsync($"po2-{Guid.NewGuid()}@test.com", SystemRole.PROCUREMENT_OFFICER);
            var id = await CreateEvaluationAsync(poToken);

            var response = await _client.DeleteAsync($"/api/vendor-evaluations/{id}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // VE-INT-06: MANAGER can delete successfully
        [Fact]
        public async Task DeleteEvaluation_AsManager_Returns204()
        {
            var poToken = await RegisterAndLoginAsync($"po3-{Guid.NewGuid()}@test.com", SystemRole.PROCUREMENT_OFFICER);
            var id = await CreateEvaluationAsync(poToken);

            var managerToken = await RegisterAndLoginAsync($"mgr-{Guid.NewGuid()}@test.com", SystemRole.MANAGER);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);

            var response = await _client.DeleteAsync($"/api/vendor-evaluations/{id}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        // VE-INT-07: fetching a nonexistent evaluation returns 404 through the real pipeline
        [Fact]
        public async Task GetById_WhenNotFound_Returns404()
        {
            var token = await RegisterAndLoginAsync($"mgr2-{Guid.NewGuid()}@test.com", SystemRole.MANAGER);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync($"/api/vendor-evaluations/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}