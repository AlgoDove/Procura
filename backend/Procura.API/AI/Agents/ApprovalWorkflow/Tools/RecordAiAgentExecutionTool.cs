using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;
using Procura.API.Modules.ApprovalWorkflow.Entities;
using Procura.API.Modules.ApprovalWorkflow.Repositories;

namespace Procura.API.AI.Agents.ApprovalWorkflow.Tools
{
    public class RecordAiAgentExecutionInput
    {
        public Guid WorkflowId { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public int ExecutionOrder { get; set; } = 1;
        public string ExecutionStatus { get; set; } = "COMPLETED";
        public string? InputSummary { get; set; }
        public string? OutputSummary { get; set; }
        public string? ValidationResult { get; set; }
        public string? ToolExecutionMetadata { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }

    public class RecordAiAgentExecutionTool : IAgentTool
    {
        private readonly IApprovalWorkflowRepository _workflowRepository;

        public string Name => "RecordAiAgentExecutionTool";
        public string Description => "Persists an immutable AIAgentExecution audit record attached to the approval workflow.";
        public bool IsMutating => true;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public RecordAiAgentExecutionTool(IApprovalWorkflowRepository workflowRepository)
        {
            _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
        }

        public async Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            try
            {
                RecordAiAgentExecutionInput? recordInput = null;

                if (input is RecordAiAgentExecutionInput directInput)
                {
                    recordInput = directInput;
                }
                else if (input != null)
                {
                    var json = JsonSerializer.Serialize(input, JsonOptions);
                    recordInput = JsonSerializer.Deserialize<RecordAiAgentExecutionInput>(json, JsonOptions);
                }

                if (recordInput == null || recordInput.WorkflowId == Guid.Empty)
                {
                    return ToolResult.Fail(Name, "Invalid input: Valid WorkflowId is required to record AI agent execution.");
                }

                var execution = new AIAgentExecution
                {
                    Id = Guid.NewGuid(),
                    ApprovalWorkflowId = recordInput.WorkflowId,
                    AgentName = string.IsNullOrWhiteSpace(recordInput.AgentName) ? "ProcurementDecisionSupportAgent" : recordInput.AgentName,
                    ExecutionOrder = recordInput.ExecutionOrder,
                    ExecutionStatus = recordInput.ExecutionStatus,
                    InputSummary = recordInput.InputSummary,
                    OutputSummary = recordInput.OutputSummary,
                    ValidationResult = recordInput.ValidationResult,
                    ToolExecutionMetadata = recordInput.ToolExecutionMetadata,
                    StartedAt = recordInput.StartedAt,
                    CompletedAt = recordInput.CompletedAt ?? DateTime.UtcNow
                };

                await _workflowRepository.AddAgentExecutionAsync(execution);
                await _workflowRepository.SaveChangesAsync();

                return ToolResult.Ok(Name, execution.Id);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail(Name, $"RecordAiAgentExecutionTool failed: {ex.Message}");
            }
        }
    }
}
