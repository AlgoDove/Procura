using System;
using System.Collections.Generic;
using System.Linq;
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
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Enums;
using Procura.API.Modules.VendorManagement.DTOs;
using Procura.API.Shared.Data;
using Procura.API.Shared.Enums;
using Xunit;

namespace Procura.API.Tests.Integration
{
    /// <summary>
    /// Comprehensive Block 10 End-to-End Integration Test verifying the complete non-AI
    /// procurement lifecycle across Component 1 (Procurement Requests), Component 2 (Vendor Management),
    /// Component 3 (Vendor Evaluation), and Component 4 (Approval Workflow Management).
    /// </summary>
    public class EndToEndProcurementLifecycleIntegrationTests : IClassFixture<VendorManagementWebApplicationFactory>
    {
        private readonly VendorManagementWebApplicationFactory _factory;
        private readonly HttpClient _client;
        private readonly JsonSerializerOptions _jsonOptions;

        public EndToEndProcurementLifecycleIntegrationTests(VendorManagementWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            _jsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        }

        private async Task<(string token, Guid userId)> RegisterAndLoginAsync(string email, SystemRole role = SystemRole.EMPLOYEE)
        {
            var registerBody = new
            {
                firstName = "Integration",
                lastName = "Tester",
                email,
                password = "Password123!"
            };

            var registerRes = await _client.PostAsJsonAsync("/api/Auth/register", registerBody);
            Assert.True(registerRes.IsSuccessStatusCode, $"Registration failed: {await registerRes.Content.ReadAsStringAsync()}");

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
            Assert.True(loginResponse.IsSuccessStatusCode, $"Login failed: {await loginResponse.Content.ReadAsStringAsync()}");

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
        public async Task CompleteProcurementLifecycle_AcrossComponents1to4_SucceedsSeamlessly()
        {
            // ---------------------------------------------------------------------------------
            // STEP 1: Set Up Personas (Employee / Officer / Manager / Admin)
            // ---------------------------------------------------------------------------------
            var empEmail = $"employee_{Guid.NewGuid():N}@procura.test";
            var offEmail = $"officer_{Guid.NewGuid():N}@procura.test";
            var mgrEmail = $"manager_{Guid.NewGuid():N}@procura.test";
            var admEmail = $"admin_{Guid.NewGuid():N}@procura.test";

            var (empToken, empId) = await RegisterAndLoginAsync(empEmail, SystemRole.EMPLOYEE);
            var (offToken, offId) = await RegisterAndLoginAsync(offEmail, SystemRole.PROCUREMENT_OFFICER);
            var (mgrToken, mgrId) = await RegisterAndLoginAsync(mgrEmail, SystemRole.MANAGER);
            var (admToken, admId) = await RegisterAndLoginAsync(admEmail, SystemRole.ADMIN);

            // ---------------------------------------------------------------------------------
            // STEP 2: Component 1 — Create Procurement Request with Line Items
            // ---------------------------------------------------------------------------------
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
            var createPrDto = new CreateProcurementRequestDto
            {
                Title = "High-Performance Compute Cluster",
                Description = "Servers and high-speed switches for AI research labs.",
                Justification = "Required for computational modeling workloads.",
                Priority = Priority.HIGH,
                RequiredByDate = DateTime.UtcNow.AddDays(30),
                Items = new List<CreateProcurementRequestItemDto>
                {
                    new CreateProcurementRequestItemDto
                    {
                        ItemName = "GPU Accelerated Server Node",
                        Description = "4x Tensor Core GPUs with 256GB ECC RAM",
                        Quantity = 2,
                        Unit = "Units",
                        EstimatedUnitPrice = 10000m
                    },
                    new CreateProcurementRequestItemDto
                    {
                        ItemName = "100GbE Managed Switch",
                        Description = "Low-latency network switch",
                        Quantity = 1,
                        Unit = "Units",
                        EstimatedUnitPrice = 5000m
                    }
                }
            };

            var prRes = await _client.PostAsJsonAsync("/api/procurement-requests", createPrDto);
            Assert.Equal(HttpStatusCode.Created, prRes.StatusCode);
            var createdPr = await prRes.Content.ReadFromJsonAsync<ProcurementRequestResponseDto>(_jsonOptions);
            Assert.NotNull(createdPr);
            Assert.Equal(25000m, createdPr.EstimatedTotal);
            Assert.Equal("DRAFT", createdPr.Status);

            // ---------------------------------------------------------------------------------
            // STEP 3: Component 2 — Register Vendors and Submit Vendor Quotes
            // ---------------------------------------------------------------------------------
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", offToken);

            // Vendor A (Top contender)
            var vendorADto = new CreateVendorDto
            {
                Name = $"Nexus Compute Labs_{Guid.NewGuid():N}",
                ContactPerson = "Sarah Jenkins",
                Email = $"sarah_{Guid.NewGuid():N}@nexus.com",
                PhoneNumber = "+1555123456",
                Category = "Hardware",
                Rating = 4.9m
            };
            var vARes = await _client.PostAsJsonAsync("/api/vendors", vendorADto);
            Assert.Equal(HttpStatusCode.Created, vARes.StatusCode);
            var vendorA = await vARes.Content.ReadFromJsonAsync<VendorResponseDto>(_jsonOptions);
            Assert.NotNull(vendorA);

            // Vendor B (Runner up)
            var vendorBDto = new CreateVendorDto
            {
                Name = $"Apex Server Solutions_{Guid.NewGuid():N}",
                ContactPerson = "Michael Chang",
                Email = $"michael_{Guid.NewGuid():N}@apex.com",
                PhoneNumber = "+1555987654",
                Category = "Hardware",
                Rating = 4.3m
            };
            var vBRes = await _client.PostAsJsonAsync("/api/vendors", vendorBDto);
            Assert.Equal(HttpStatusCode.Created, vBRes.StatusCode);
            var vendorB = await vBRes.Content.ReadFromJsonAsync<VendorResponseDto>(_jsonOptions);
            Assert.NotNull(vendorB);

            // Submit Quotes for both vendors
            var quoteADto = new CreateVendorQuoteDto(
                createdPr.Id,
                vendorA.Id,
                vendorA.Name,
                23800m,
                8,
                95m,
                true,
                "Comprehensive enterprise warranty included."
            );
            var qARes = await _client.PostAsJsonAsync("/api/vendor-quotes", quoteADto);
            Assert.Equal(HttpStatusCode.Created, qARes.StatusCode);

            var quoteBDto = new CreateVendorQuoteDto(
                createdPr.Id,
                vendorB.Id,
                vendorB.Name,
                24900m,
                15,
                85m,
                true,
                "Standard standard delivery."
            );
            var qBRes = await _client.PostAsJsonAsync("/api/vendor-quotes", quoteBDto);
            Assert.Equal(HttpStatusCode.Created, qBRes.StatusCode);

            // ---------------------------------------------------------------------------------
            // STEP 4: Component 4 — Initialize Approval Workflow (SUBMITTED)
            // ---------------------------------------------------------------------------------
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
            var initWorkflowDto = new CreateApprovalWorkflowDto { ProcurementRequestId = createdPr.Id };
            var initRes = await _client.PostAsJsonAsync("/api/approval-workflows/initialize", initWorkflowDto);
            Assert.Equal(HttpStatusCode.Created, initRes.StatusCode);
            var workflow = await initRes.Content.ReadFromJsonAsync<ApprovalWorkflowResponseDto>(_jsonOptions);
            Assert.NotNull(workflow);
            Assert.Equal("SUBMITTED", workflow.CurrentStatus);

            // ---------------------------------------------------------------------------------
            // STEP 5: Component 3 — Vendor Evaluation and Scoring
            // ---------------------------------------------------------------------------------
            // Advance stage to UNDER_VENDOR_EVALUATION
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", offToken);
            var trans1 = new TransitionWorkflowRequestDto { TargetStatus = WorkflowState.UNDER_VENDOR_EVALUATION };
            var trans1Res = await _client.PostAsJsonAsync($"/api/approval-workflows/{workflow.Id}/transition", trans1);
            Assert.Equal(HttpStatusCode.OK, trans1Res.StatusCode);

            // Run automated vendor evaluation
            var evalRequest = new EvaluateVendorsRequestDto
            {
                ProcurementRequestId = createdPr.Id,
                EstimatedBudget = 25000m,
                RequiredDeliveryDays = 12,
                CustomWeights = new List<CriterionWeightConfigDto>
                {
                    new CriterionWeightConfigDto { Criterion = EvaluationCriterionType.PRICE, Weight = 0.40m },
                    new CriterionWeightConfigDto { Criterion = EvaluationCriterionType.DELIVERY_TIME, Weight = 0.25m },
                    new CriterionWeightConfigDto { Criterion = EvaluationCriterionType.RELIABILITY, Weight = 0.20m },
                    new CriterionWeightConfigDto { Criterion = EvaluationCriterionType.COMPLIANCE, Weight = 0.15m }
                },
                CandidateVendors = new List<CandidateVendorMetricDto>
                {
                    new CandidateVendorMetricDto
                    {
                        VendorId = vendorA.Id,
                        VendorName = vendorA.Name,
                        QuotedPrice = 23800m,
                        EstimatedDeliveryDays = 8,
                        ReliabilityRating = 95m,
                        IsComplianceApproved = true
                    },
                    new CandidateVendorMetricDto
                    {
                        VendorId = vendorB.Id,
                        VendorName = vendorB.Name,
                        QuotedPrice = 24900m,
                        EstimatedDeliveryDays = 15,
                        ReliabilityRating = 85m,
                        IsComplianceApproved = true
                    }
                }
            };

            var evalRes = await _client.PostAsJsonAsync("/api/vendor-evaluations/evaluate", evalRequest);
            Assert.Equal(HttpStatusCode.OK, evalRes.StatusCode);
            var evalSummary = await evalRes.Content.ReadFromJsonAsync<ProcurementEvaluationSummaryDto>(_jsonOptions);
            Assert.NotNull(evalSummary);
            Assert.Equal(vendorA.Id, evalSummary.TopRecommendedVendorId);
            Assert.Equal(2, evalSummary.RankedEvaluations.Count);
            Assert.Equal(1, evalSummary.RankedEvaluations[0].Rank);
            Assert.Equal(vendorA.Id, evalSummary.RankedEvaluations[0].VendorId);

            // Advance stages: AI_RECOMMENDATION_GENERATED -> WAITING_MANAGER_APPROVAL
            var trans2 = new TransitionWorkflowRequestDto { TargetStatus = WorkflowState.AI_RECOMMENDATION_GENERATED };
            var trans2Res = await _client.PostAsJsonAsync($"/api/approval-workflows/{workflow.Id}/transition", trans2);
            Assert.Equal(HttpStatusCode.OK, trans2Res.StatusCode);

            var trans3 = new TransitionWorkflowRequestDto { TargetStatus = WorkflowState.WAITING_MANAGER_APPROVAL };
            var trans3Res = await _client.PostAsJsonAsync($"/api/approval-workflows/{workflow.Id}/transition", trans3);
            Assert.Equal(HttpStatusCode.OK, trans3Res.StatusCode);

            // ---------------------------------------------------------------------------------
            // STEP 6: Component 4 — Manager Review with Vendor Recommendation Summary
            // ---------------------------------------------------------------------------------
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);

            // Fetch pending approvals
            var pendingRes = await _client.GetAsync("/api/approval-workflows/pending");
            Assert.Equal(HttpStatusCode.OK, pendingRes.StatusCode);
            var pendingList = await pendingRes.Content.ReadFromJsonAsync<List<ApprovalWorkflowResponseDto>>(_jsonOptions);
            Assert.NotNull(pendingList);
            var pendingWorkflow = pendingList.FirstOrDefault(w => w.Id == workflow.Id);
            Assert.NotNull(pendingWorkflow);
            Assert.Equal("WAITING_MANAGER_APPROVAL", pendingWorkflow.CurrentStatus);
            // Verify Component 3 summary is cleanly surfaced to Manager
            Assert.NotNull(pendingWorkflow.VendorRecommendationSummary);
            Assert.Equal(vendorA.Id, pendingWorkflow.VendorRecommendationSummary.TopRecommendedVendorId);
           Assert.Contains(
    vendorA.Name,
    pendingWorkflow.VendorRecommendationSummary.RecommendationSummary
);

            // Inspect audit trail prior to decision
            var auditRes = await _client.GetAsync($"/api/approval-workflows/{workflow.Id}/audit-trail");
            Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);
            var audit = await auditRes.Content.ReadFromJsonAsync<WorkflowAuditTrailDto>(_jsonOptions);
            Assert.NotNull(audit);
            Assert.Equal(createdPr.RequestNumber, audit.RequestNumber);

            // ---------------------------------------------------------------------------------
            // STEP 7: Security Invariant Verification — Role Isolation
            // ---------------------------------------------------------------------------------
            // Employee cannot approve
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
            var empApprove = await _client.PostAsJsonAsync($"/api/approval-workflows/{workflow.Id}/approve",
                new ApprovalDecisionRequestDto { Comments = "Employee cannot approve" });
            Assert.Equal(HttpStatusCode.Forbidden, empApprove.StatusCode);

            // Officer cannot approve
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", offToken);
            var offApprove = await _client.PostAsJsonAsync($"/api/approval-workflows/{workflow.Id}/approve",
                new ApprovalDecisionRequestDto { Comments = "Officer cannot approve" });
            Assert.Equal(HttpStatusCode.Forbidden, offApprove.StatusCode);

            // Admin cannot approve
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admToken);
            var admApprove = await _client.PostAsJsonAsync($"/api/approval-workflows/{workflow.Id}/approve",
                new ApprovalDecisionRequestDto { Comments = "Admin cannot approve" });
            Assert.Equal(HttpStatusCode.Forbidden, admApprove.StatusCode);

            // ---------------------------------------------------------------------------------
            // STEP 8: Manager Operational Approval
            // ---------------------------------------------------------------------------------
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
            var approveDecision = new ApprovalDecisionRequestDto
            {
                Comments = "Approved. Nexus Compute Labs offers superior delivery turnaround within allocated budget."
            };
            var approveRes = await _client.PostAsJsonAsync($"/api/approval-workflows/{workflow.Id}/approve", approveDecision);
            Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
            var approvedWorkflow = await approveRes.Content.ReadFromJsonAsync<ApprovalWorkflowResponseDto>(_jsonOptions);
            Assert.NotNull(approvedWorkflow);
            Assert.Equal("APPROVED", approvedWorkflow.CurrentStatus);

            // Verify Procurement Request status is synchronized to APPROVED
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
            var getPrRes = await _client.GetAsync($"/api/procurement-requests/{createdPr.Id}");
            Assert.Equal(HttpStatusCode.OK, getPrRes.StatusCode);
            var reloadedPr = await getPrRes.Content.ReadFromJsonAsync<ProcurementRequestResponseDto>(_jsonOptions);
            Assert.NotNull(reloadedPr);
            Assert.Equal("APPROVED", reloadedPr.Status);

            // ---------------------------------------------------------------------------------
            // STEP 9: Procurement Officer Fulfills Request (COMPLETED)
            // ---------------------------------------------------------------------------------
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", offToken);
            var transComplete = new TransitionWorkflowRequestDto { TargetStatus = WorkflowState.COMPLETED };
            var transCompleteRes = await _client.PostAsJsonAsync($"/api/approval-workflows/{workflow.Id}/transition", transComplete);
            Assert.Equal(HttpStatusCode.OK, transCompleteRes.StatusCode);
            var completedWorkflow = await transCompleteRes.Content.ReadFromJsonAsync<ApprovalWorkflowResponseDto>(_jsonOptions);
            Assert.NotNull(completedWorkflow);
            Assert.Equal("COMPLETED", completedWorkflow.CurrentStatus);
            Assert.NotNull(completedWorkflow.CompletedAt);

            // ---------------------------------------------------------------------------------
            // STEP 10: Requester Notification Center Verification
            // ---------------------------------------------------------------------------------
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
            var notifsRes = await _client.GetAsync("/api/approval-workflows/notifications");
            Assert.Equal(HttpStatusCode.OK, notifsRes.StatusCode);
            var notifs = await notifsRes.Content.ReadFromJsonAsync<List<NotificationResponseDto>>(_jsonOptions);
            Assert.NotNull(notifs);
            Assert.True(notifs.Count >= 3, $"Expected at least 3 notifications, received {notifs.Count}");

            // Verify unread count
            var countRes = await _client.GetAsync("/api/approval-workflows/notifications/unread-count");
            Assert.Equal(HttpStatusCode.OK, countRes.StatusCode);
            using (var countDoc1 = JsonDocument.Parse(await countRes.Content.ReadAsStringAsync()))
            {
                var initialUnreadCount = countDoc1.RootElement.GetProperty("unreadCount").GetInt32();
                Assert.True(initialUnreadCount > 0);

                // Mark one notification as read
                var notifToRead = notifs.First();
                var readRes = await _client.PatchAsync($"/api/approval-workflows/notifications/{notifToRead.Id}/read", null);
                Assert.Equal(HttpStatusCode.NoContent, readRes.StatusCode);

                // Verify unread count decreased
                var countAfterRes = await _client.GetAsync("/api/approval-workflows/notifications/unread-count");
                using var countDoc2 = JsonDocument.Parse(await countAfterRes.Content.ReadAsStringAsync());
                var finalUnreadCount = countDoc2.RootElement.GetProperty("unreadCount").GetInt32();
                Assert.Equal(initialUnreadCount - 1, finalUnreadCount);
            }

            // ---------------------------------------------------------------------------------
            // STEP 11: Final Audit Trail Complete Inspection
            // ---------------------------------------------------------------------------------
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
            var finalAuditRes = await _client.GetAsync($"/api/approval-workflows/{workflow.Id}/audit-trail");
            Assert.Equal(HttpStatusCode.OK, finalAuditRes.StatusCode);
            var finalAudit = await finalAuditRes.Content.ReadFromJsonAsync<WorkflowAuditTrailDto>(_jsonOptions);
            Assert.NotNull(finalAudit);
            Assert.Equal("COMPLETED", finalAudit.CurrentStatus);
            Assert.Single(finalAudit.Decisions);
            Assert.Equal("APPROVED", finalAudit.Decisions[0].Decision);
            Assert.Equal(mgrId, finalAudit.Decisions[0].ManagerId);
            Assert.Contains("Nexus Compute Labs", finalAudit.Decisions[0].Comments);
        }
    }
}
