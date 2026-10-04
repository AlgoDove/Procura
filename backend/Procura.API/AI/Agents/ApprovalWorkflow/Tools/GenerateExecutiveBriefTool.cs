using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;

namespace Procura.API.AI.Agents.ApprovalWorkflow.Tools
{
    public class GenerateExecutiveBriefInput
    {
        public Guid WorkflowId { get; set; }
        public ApprovalReadinessData ReadinessData { get; set; } = new();
        public ExtractedDecisionSupportInput ExtractedAdvice { get; set; } = new();
    }

    public class GenerateExecutiveBriefTool : IAgentTool
    {
        public string Name => "GenerateExecutiveBriefTool";
        public string Description => "Synthesizes deterministic readiness metrics, cost variances, delivery SLAs, and LLM advice into an executive decision memo for the manager.";
        public bool IsMutating => false;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            try
            {
                GenerateExecutiveBriefInput? briefInput = null;

                if (input is GenerateExecutiveBriefInput directInput)
                {
                    briefInput = directInput;
                }
                else if (input != null)
                {
                    var json = JsonSerializer.Serialize(input, JsonOptions);
                    briefInput = JsonSerializer.Deserialize<GenerateExecutiveBriefInput>(json, JsonOptions);
                }

                if (briefInput == null || briefInput.ReadinessData == null)
                {
                    return Task.FromResult(ToolResult.Fail(Name, "Invalid input: ReadinessData is required."));
                }

                var r = briefInput.ReadinessData;
                var a = briefInput.ExtractedAdvice ?? new ExtractedDecisionSupportInput();

                var isWithinBudget = !r.TopQuotedPrice.HasValue || r.TopQuotedPrice.Value <= r.EstimatedTotal;
                var recommendedAmount = r.TopQuotedPrice ?? r.EstimatedTotal;

                var riskList = new List<string>(a.RiskFactors ?? new List<string>());
                if (!isWithinBudget && r.TopQuotedPrice.HasValue)
                {
                    var diff = r.TopQuotedPrice.Value - r.EstimatedTotal;
                    riskList.Add($"Cost Variance: Quoted price exceeds budget by ${diff:N2}.");
                }
                if (!r.IsComplianceApproved)
                {
                    riskList.Add("Compliance Warning: Candidate has pending or failed compliance review.");
                }

                var briefOutput = new DecisionSupportOutput
                {
                    ProcurementRequestId = r.ProcurementRequestId,
                    WorkflowId = briefInput.WorkflowId != Guid.Empty ? briefInput.WorkflowId : context.WorkflowId,
                    RequestNumber = r.RequestNumber,
                    RequestTitle = r.Title,
                    RecommendedAction = string.IsNullOrWhiteSpace(a.RecommendedAction) ? "APPROVE" : a.RecommendedAction.ToUpperInvariant().Trim(),
                    TopVendorId = r.TopVendorId,
                    TopVendorName = r.TopVendorName,
                    RecommendedAmount = recommendedAmount,
                    BudgetCap = r.EstimatedTotal,
                    IsWithinBudget = isWithinBudget,
                    IsComplianceVerified = r.IsComplianceApproved,
                    ConfidenceScore = a.ConfidenceScore > 0 ? a.ConfidenceScore : 88.0m,
                    ExecutiveBrief = !string.IsNullOrWhiteSpace(a.ExecutiveSummary)
                        ? a.ExecutiveSummary
                        : $"Recommended approval for {r.TopVendorName ?? "top candidate"} at ${recommendedAmount:N2} with overall scoring of {r.TopScore ?? 0:N1}/100.",
                    RiskFactors = riskList,
                    KeyTradeoffs = a.KeyTradeoffs ?? new List<string>(),
                    ConditionsOrStipulations = a.ConditionsOrStipulations ?? new List<string>(),
                    GeneratedByAgent = true,
                    GeneratedAt = DateTime.UtcNow
                };

                return Task.FromResult(ToolResult.Ok(Name, briefOutput));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Fail(Name, $"GenerateExecutiveBriefTool failed: {ex.Message}"));
            }
        }
    }
}
