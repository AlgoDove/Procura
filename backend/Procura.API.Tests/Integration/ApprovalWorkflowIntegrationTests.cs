using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procura.API.Modules.ApprovalWorkflow.DTOs;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Modules.ProcurementRequest.DTOs;
using Procura.API.Modules.ProcurementRequest.Enums;
using Procura.API.Shared.Data;
using Procura.API.Shared.Enums;
using Xunit;

namespace Procura.API.Tests.Integration
{
    public class ApprovalWorkflowIntegrationTests : IClassFixture<VendorManagementWebApplicationFactory>
    {
        private readonly VendorManagementWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public ApprovalWorkflowIntegrationTests(VendorManagementWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(string token, Guid userId)> RegisterAndLoginAsync(string email, SystemRole role = SystemRole.EMPLOYEE)
        {
            var registerBody = new
            {
                firstName = "Test",
                lastName = "User",
                email,
                password = "Password123!"
            };

            await _client.PostAsJsonAsync("/api/Auth/register", registerBody);

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.FirstAsync(u => u.Email == email);
                if (role != SystemRole.EMPLOYEE)
                {
                    user.Role = role;
                    await db.SaveChangesAsync();
                }
            }

            var loginBody = new { email, password = "Password123!" };
            var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", loginBody);
            var loginJson = await loginResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(loginJson);
            var token = doc.RootElement.GetProperty("token").GetString()!;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.FirstAsync(u => u.Email == email);
                return (token, user.Id);
            }
        }

        [Fact]
        public async Task EndToEnd_ApprovalLifecycle_IntegrationTest()
        {
            // 1. Register test users for different roles
            var employeeEmail = $"emp_{Guid.NewGuid():N}@procura.test";
            var managerEmail = $"mgr_{Guid.NewGuid():N}@procura.test";
            var officerEmail = $"off_{Guid.NewGuid():N}@procura.test";

            var (empToken, empId) = await RegisterAndLoginAsync(employeeEmail, SystemRole.EMPLOYEE);
            var (mgrToken, mgrId) = await RegisterAndLoginAsync(managerEmail, SystemRole.MANAGER);
            var (offToken, offId) = await RegisterAndLoginAsync(officerEmail, SystemRole.PROCUREMENT_OFFICER);

            // 2. Employee creates a procurement request
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
            var createRequestDto = new CreateProcurementRequestDto
            {
                Title = "High-end developer laptops",
                Description = "Laptops for engineering team",
                Justification = "New team onboarding",
                Priority = Priority.HIGH,
                RequiredByDate = DateTime.UtcNow.AddDays(14),
                Items = new()
                {
                    new CreateProcurementRequestItemDto
                    {
                        ItemName = "MacBook Pro 16",
                        Description = "M3 Max, 36GB RAM",
                        Quantity = 3,
                        Unit = "pcs",
                        EstimatedUnitPrice = 3500m
                    }
                }
            };

            var createReqRes = await _client.PostAsJsonAsync("/api/procurement-requests", createRequestDto);
            Assert.Equal(HttpStatusCode.Created, createReqRes.StatusCode);
            var createdReq = await createReqRes.Content.ReadFromJsonAsync<ProcurementRequestResponseDto>();
            Assert.NotNull(createdReq);

            // 3. Employee initializes the approval workflow
            var initWorkflowDto = new CreateApprovalWorkflowDto { ProcurementRequestId = createdReq.Id };
            var initRes = await _client.PostAsJsonAsync("/api/approval-workflows/initialize", initWorkflowDto);
            Assert.Equal(HttpStatusCode.Created, initRes.StatusCode);
            var workflow = await initRes.Content.ReadFromJsonAsync<ApprovalWorkflowResponseDto>();
            Assert.NotNull(workflow);
            Assert.Equal("SUBMITTED", workflow.CurrentStatus);

            // 4. Procurement Officer advances stage to UNDER_VENDOR_EVALUATION
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", offToken);
            var transition1 = new TransitionWorkflowRequestDto { TargetStatus = WorkflowState.UNDER_VENDOR_EVALUATION };
            var trans1Res = await _client.PostAsJsonAsync($"/api/approval-workflows/{workflow.Id}/transition", transition1);
            Assert.Equal(HttpStatusCode.OK, trans1Res.StatusCode);

            // 5. Procurement Officer advances stage to AI_RECOMMENDATION_GENERATED then WAITING_MANAGER_APPROVAL
            var transition2 = new TransitionWorkflowRequestDto { TargetStatus = WorkflowState.AI_RECOMMENDATION_GENERATED };
            var trans2Res = await _client.PostAsJsonAsync($"/api/approval-workflows/{workflow.Id}/transition", transition2);
            var trans2Body = await trans2Res.Content.ReadAsStringAsync();
            Assert.True(trans2Res.IsSuccessStatusCode, $"trans2 failed: {trans2Res.StatusCode} - {trans2Body}");

            var transition3 = new TransitionWorkflowRequestDto { TargetStatus = WorkflowState.WAITING_MANAGER_APPROVAL };
            var trans3Res = await _client.PostAsJsonAsync($"/api/approval-workflows/{workflow.Id}/transition", transition3);
            var trans3Body = await trans3Res.Content.ReadAsStringAsync();
            Assert.True(trans3Res.IsSuccessStatusCode, $"trans3 failed: {trans3Res.StatusCode} - {trans3Body}");

            // 6. Security Check: Employee attempts to approve -> Must receive 403 Forbidden!
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
            var unauthorizedApproveRes = await _client.PostAsJsonAsync(
                $"/api/approval-workflows/{workflow.Id}/approve",
                new ApprovalDecisionRequestDto { Comments = "Employee sneaking approval" });
            Assert.Equal(HttpStatusCode.Forbidden, unauthorizedApproveRes.StatusCode);

            // 7. Security Check: Procurement Officer attempts to approve -> Must receive 403 Forbidden!
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", offToken);
            var officerApproveRes = await _client.PostAsJsonAsync(
                $"/api/approval-workflows/{workflow.Id}/approve",
                new ApprovalDecisionRequestDto { Comments = "Officer attempting approval" });
            Assert.Equal(HttpStatusCode.Forbidden, officerApproveRes.StatusCode);

            // 8. Authorized Manager approves -> 200 OK!
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
            var managerApproveRes = await _client.PostAsJsonAsync(
                $"/api/approval-workflows/{workflow.Id}/approve",
                new ApprovalDecisionRequestDto { Comments = "Approved in accordance with Q3 hardware plan." });
            Assert.Equal(HttpStatusCode.OK, managerApproveRes.StatusCode);
            var approvedWorkflow = await managerApproveRes.Content.ReadFromJsonAsync<ApprovalWorkflowResponseDto>();
            Assert.NotNull(approvedWorkflow);
            Assert.Equal("APPROVED", approvedWorkflow.CurrentStatus);
            Assert.Single(approvedWorkflow.Decisions);
            Assert.Equal("Approved in accordance with Q3 hardware plan.", approvedWorkflow.Decisions[0].Comments);

            // 9. Verify Audit Trail endpoint
            var auditRes = await _client.GetAsync($"/api/approval-workflows/{workflow.Id}/audit-trail");
            Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);
            var audit = await auditRes.Content.ReadFromJsonAsync<WorkflowAuditTrailDto>();
            Assert.NotNull(audit);
            Assert.Equal("APPROVED", audit.CurrentStatus);
            Assert.Single(audit.Decisions);

            // 10. Verify Employee receives notification
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
            var notifRes = await _client.GetAsync("/api/approval-workflows/notifications");
            Assert.Equal(HttpStatusCode.OK, notifRes.StatusCode);
        }
    }
}
