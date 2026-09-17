using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Procura.API.AI.Agents.ProcurementRequest;
using Procura.API.AI.Core;
using Procura.API.AI.Entities;
using Procura.API.AI.Persistence;

namespace Procura.API.AI.Orchestration
{
    public class CentralOrchestrator : IWorkflowOrchestrator
    {
        private readonly IWorkflowRepository _workflowRepository;
        private readonly IProcurementRequestAgent _procurementAgent;
        private readonly ILogger<CentralOrchestrator> _logger;

        private const int MaxExecutionCycles = 3;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public CentralOrchestrator(
            IWorkflowRepository workflowRepository,
            IProcurementRequestAgent procurementAgent,
            ILogger<CentralOrchestrator> logger)
        {
            _workflowRepository = workflowRepository;
            _procurementAgent = procurementAgent;
            _logger = logger;
        }

        public async Task<WorkflowContext> ProcessWorkflowAsync(
            string objective,
            Guid requesterId,
            string requesterRole,
            Guid? existingRequestId = null,
            Guid? workflowId = null,
            CancellationToken ct = default)
        {
            WorkflowContext context;
            WorkflowInstance? instance = null;

            if (workflowId.HasValue)
            {
                instance = await _workflowRepository.GetByIdAsync(workflowId.Value);
                if (instance != null)
                {
                    if (requesterRole == "EMPLOYEE" && instance.RequesterId != requesterId)
                    {
                        throw new UnauthorizedAccessException("Unauthorized: Cannot access another user's workflow.");
                    }

                    context = !string.IsNullOrEmpty(instance.ContextJson)
                        ? JsonSerializer.Deserialize<WorkflowContext>(instance.ContextJson, JsonOptions) ?? new WorkflowContext()
                        : new WorkflowContext();

                    context.WorkflowId = instance.Id;
                    context.RequesterId = requesterId;
                    context.RequesterRole = requesterRole;
                    context.Objective = objective;
                    context.ExistingRequestId = existingRequestId ?? instance.ProcurementRequestId;
                }
                else
                {
                    context = InitializeNewWorkflow(objective, requesterId, requesterRole, existingRequestId, workflowId.Value);
                }
            }
            else
            {
                context = InitializeNewWorkflow(objective, requesterId, requesterRole, existingRequestId, Guid.NewGuid());
            }

            // Enforce execution cycle limit
            int cycles = 0;

            while (cycles < MaxExecutionCycles)
            {
                cycles++;

                if (context.CurrentStage == WorkflowStage.PROCUREMENT_REQUEST)
                {
                    var step1 = context.Plan.Steps.FirstOrDefault(s => s.Stage == WorkflowStage.PROCUREMENT_REQUEST);
                    if (step1 != null)
                    {
                        step1.Status = StepStatus.IN_PROGRESS;
                        step1.StartedAt = DateTime.UtcNow;
                    }

                    context.AddAudit(
                        WorkflowStage.PROCUREMENT_REQUEST,
                        "CentralOrchestrator",
                        "AGENT_DELEGATED",
                        "DELEGATED",
                        $"Delegating PROCUREMENT_REQUEST task to {_procurementAgent.AgentName}.");

                    var agentResult = await _procurementAgent.ExecuteAsync(context, ct);

                    if (agentResult.Status == AgentExecutionStatus.NEEDS_USER_INPUT)
                    {
                        if (step1 != null)
                        {
                            step1.Status = StepStatus.PENDING;
                            step1.OutcomeSummary = agentResult.ExecutionSummary;
                        }

                        context.Status = WorkflowStatus.NEEDS_USER_INPUT;
                        context.ClarificationPrompt = agentResult.ClarificationPrompt;
                        context.ExecutionSummary = agentResult.ExecutionSummary;

                        context.AddAudit(
                            WorkflowStage.PROCUREMENT_REQUEST,
                            "CentralOrchestrator",
                            "WORKFLOW_PAUSED",
                            "NEEDS_USER_INPUT",
                            "Workflow paused awaiting additional user input.");

                        break; // Stop safely and return to user
                    }
                    else if (agentResult.Status == AgentExecutionStatus.FAILED)
                    {
                        if (step1 != null)
                        {
                            step1.Status = StepStatus.FAILED;
                            step1.OutcomeSummary = agentResult.ExecutionSummary;
                        }

                        context.Status = WorkflowStatus.FAILED;
                        context.Errors.AddRange(agentResult.ErrorMessages);
                        context.Errors.AddRange(agentResult.ValidationErrors);
                        context.ExecutionSummary = agentResult.ExecutionSummary;

                        context.AddAudit(
                            WorkflowStage.PROCUREMENT_REQUEST,
                            "CentralOrchestrator",
                            "WORKFLOW_FAILED",
                            "FAILED",
                            $"Workflow failed during PROCUREMENT_REQUEST stage: {agentResult.ExecutionSummary}");

                        break; // Stop on failure
                    }
                    else if (agentResult.Status == AgentExecutionStatus.COMPLETED)
                    {
                        if (step1 != null)
                        {
                            step1.Status = StepStatus.COMPLETED;
                            step1.CompletedAt = DateTime.UtcNow;
                            step1.OutcomeSummary = agentResult.ExecutionSummary;
                        }

                        context.ProcurementRequestId = agentResult.ProcurementRequestId;
                        context.RequestNumber = agentResult.RequestNumber;
                        context.EstimatedTotal = agentResult.EstimatedTotal;
                        context.ExecutionSummary = agentResult.ExecutionSummary;

                        // Stage 1 is complete; workflow transitions cleanly to next stage
                        context.Status = WorkflowStatus.STAGE_COMPLETED;
                        context.CurrentStage = WorkflowStage.VENDOR_SELECTION;

                        context.AddAudit(
                            WorkflowStage.PROCUREMENT_REQUEST,
                            "CentralOrchestrator",
                            "WORKFLOW_COMPLETED",
                            "STAGE_COMPLETED",
                            $"Procurement Request Agent completed successfully. DRAFT Request: {context.RequestNumber}. Ready for future Vendor Management Agent handoff.");

                        break; // Step 1 complete; future steps remain PLANNED / NOT_STARTED
                    }
                }
                else
                {
                    // Future agents (Steps 2-4) will plug in here
                    _logger.LogInformation("Workflow stage {Stage} is planned for future agent integration.", context.CurrentStage);
                    break;
                }
            }

            // Persist durable workflow state in PostgreSQL
            await SaveWorkflowStateAsync(context, instance);

            return context;
        }

        public async Task<WorkflowContext?> GetWorkflowAsync(Guid workflowId, Guid requesterId, string requesterRole)
        {
            var instance = await _workflowRepository.GetByIdAsync(workflowId);
            if (instance == null) return null;

            if (requesterRole == "EMPLOYEE" && instance.RequesterId != requesterId)
            {
                throw new UnauthorizedAccessException("Unauthorized: Cannot access another user's workflow.");
            }

            if (string.IsNullOrEmpty(instance.ContextJson))
                return null;

            return JsonSerializer.Deserialize<WorkflowContext>(instance.ContextJson, JsonOptions);
        }

        private static WorkflowContext InitializeNewWorkflow(
            string objective,
            Guid requesterId,
            string requesterRole,
            Guid? existingRequestId,
            Guid workflowId)
        {
            var context = new WorkflowContext
            {
                WorkflowId = workflowId,
                Objective = objective,
                RequesterId = requesterId,
                RequesterRole = requesterRole,
                ExistingRequestId = existingRequestId,
                CurrentStage = WorkflowStage.PROCUREMENT_REQUEST,
                Status = WorkflowStatus.IN_PROGRESS,
                Plan = WorkflowPlan.CreateDefaultPlan()
            };

            context.AddAudit(
                WorkflowStage.PROCUREMENT_REQUEST,
                "CentralOrchestrator",
                "WORKFLOW_CREATED",
                "INITIALIZED",
                $"Workflow initialized for requester {requesterId}.");

            context.AddAudit(
                WorkflowStage.PROCUREMENT_REQUEST,
                "CentralOrchestrator",
                "PLAN_CREATED",
                "INITIALIZED",
                "4-step multi-agent structured plan initialized (Step 1 active; Steps 2-4 planned).");

            return context;
        }

        private async Task SaveWorkflowStateAsync(WorkflowContext context, WorkflowInstance? existingInstance)
        {
            var planJson = JsonSerializer.Serialize(context.Plan, JsonOptions);
            var contextJson = JsonSerializer.Serialize(context, JsonOptions);
            var auditJson = JsonSerializer.Serialize(context.AuditTrail, JsonOptions);

            if (existingInstance == null)
            {
                existingInstance = new WorkflowInstance
                {
                    Id = context.WorkflowId,
                    Objective = context.Objective,
                    RequesterId = context.RequesterId,
                    RequesterRole = context.RequesterRole,
                    CurrentStage = context.CurrentStage.ToString(),
                    Status = context.Status.ToString(),
                    ProcurementRequestId = context.ProcurementRequestId,
                    RequestNumber = context.RequestNumber,
                    EstimatedTotal = context.EstimatedTotal,
                    PlanJson = planJson,
                    ContextJson = contextJson,
                    AuditTrailJson = auditJson,
                    CreatedAt = context.CreatedAt,
                    UpdatedAt = DateTime.UtcNow
                };

                await _workflowRepository.AddAsync(existingInstance);
            }
            else
            {
                existingInstance.Objective = context.Objective;
                existingInstance.CurrentStage = context.CurrentStage.ToString();
                existingInstance.Status = context.Status.ToString();
                existingInstance.ProcurementRequestId = context.ProcurementRequestId;
                existingInstance.RequestNumber = context.RequestNumber;
                existingInstance.EstimatedTotal = context.EstimatedTotal;
                existingInstance.PlanJson = planJson;
                existingInstance.ContextJson = contextJson;
                existingInstance.AuditTrailJson = auditJson;
                existingInstance.UpdatedAt = DateTime.UtcNow;

                _workflowRepository.Update(existingInstance);
            }

            await _workflowRepository.SaveChangesAsync();
        }
    }
}
