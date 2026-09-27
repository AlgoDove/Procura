using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Procura.API.AI.Agents.ApprovalWorkflow.Tools;
using Procura.API.AI.Core;
using Procura.API.AI.Gemini;
using Procura.API.Modules.ApprovalWorkflow.Repositories;

namespace Procura.API.AI.Agents.ApprovalWorkflow
{
    public class ProcurementDecisionSupportAgent : IProcurementDecisionSupportAgent
    {
        private readonly IGeminiClient _geminiClient;
        private readonly ToolRegistry _toolRegistry;
        private readonly ApprovalDecisionDeterministicValidator _validator;
        private readonly IApprovalWorkflowRepository _workflowRepository;
        private readonly ILogger<ProcurementDecisionSupportAgent> _logger;

        public string AgentName => "ProcurementDecisionSupportAgent";
        public WorkflowStage Stage => WorkflowStage.APPROVAL_WORKFLOW;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public ProcurementDecisionSupportAgent(
            IGeminiClient geminiClient,
            ToolRegistry toolRegistry,
            ApprovalDecisionDeterministicValidator validator,
            IApprovalWorkflowRepository workflowRepository,
            ILogger<ProcurementDecisionSupportAgent> logger)
        {
            _geminiClient = geminiClient ?? throw new ArgumentNullException(nameof(geminiClient));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AgentResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default)
        {
            var startedAt = DateTime.UtcNow;
            context.AddAudit(Stage, AgentName, "AGENT_STARTED", "IN_PROGRESS", "Procurement Decision Support Agent commenced analysis.");

            var toolMetadata = new List<string>();

            // ---------------------------------------------------------------------------------
            // STEP 1: Gather multi-stage procurement context via allow-listed tool
            // ---------------------------------------------------------------------------------
            var requestId = context.ExistingRequestId ?? Guid.Empty;
            if (requestId == Guid.Empty)
            {
                var error = "Cannot execute decision support: No target ProcurementRequestId provided in workflow context.";
                _logger.LogWarning("{Error}", error);
                context.AddAudit(Stage, AgentName, "READINESS_CHECK_FAILED", "NEEDS_USER_INPUT", error);
                return new AgentResult
                {
                    Status = AgentExecutionStatus.NEEDS_USER_INPUT,
                    ExecutionSummary = error,
                    ClarificationPrompt = "Please specify the target procurement request ID for approval evaluation."
                };
            }

            var toolCtx = new ToolExecutionContext
            {
                WorkflowId = context.WorkflowId,
                RequesterId = context.RequesterId,
                RequesterRole = context.RequesterRole
            };

            var readinessToolResult = await _toolRegistry.ExecuteToolAsync(
                "EvaluateApprovalReadinessTool",
                requestId,
                toolCtx,
                ct);

            toolMetadata.Add("EvaluateApprovalReadinessTool");

            if (!readinessToolResult.Success || readinessToolResult.Data is not ApprovalReadinessData readinessData)
            {
                var failError = readinessToolResult.ErrorMessage ?? "Failed to collect approval readiness data.";
                _logger.LogError("Readiness tool failed: {Error}", failError);
                context.AddAudit(Stage, AgentName, "AGENT_FAILED", "FAILED", failError);
                return new AgentResult
                {
                    Status = AgentExecutionStatus.FAILED,
                    ErrorMessages = new List<string> { failError },
                    ExecutionSummary = failError
                };
            }

            // ---------------------------------------------------------------------------------
            // STEP 2: Deterministic Pre-Validation
            // ---------------------------------------------------------------------------------
            var (isReady, readinessErrors, readinessWarnings) = _validator.ValidateReadiness(readinessData);
            if (!isReady)
            {
                var combinedErrors = string.Join("; ", readinessErrors);
                _logger.LogWarning("Approval readiness validation failed: {Errors}", combinedErrors);
                context.AddAudit(Stage, AgentName, "VALIDATION_FAILED", "NEEDS_USER_INPUT", combinedErrors);
                return new AgentResult
                {
                    Status = AgentExecutionStatus.NEEDS_USER_INPUT,
                    ValidationErrors = readinessErrors,
                    ExecutionSummary = $"Prerequisites not satisfied for manager approval: {combinedErrors}",
                    ClarificationPrompt = $"The request cannot proceed to manager review yet: {combinedErrors}"
                };
            }

            foreach (var warning in readinessWarnings)
            {
                context.AddAudit(Stage, AgentName, "POLICY_WARNING", "WARNING", warning);
            }

            // ---------------------------------------------------------------------------------
            // STEP 3: Gemini LLM Synthesis with Deterministic Fallback
            // ---------------------------------------------------------------------------------
            ExtractedDecisionSupportInput extractedAdvice;
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(readinessData, readinessWarnings, context.Objective);

            var geminiResult = await _geminiClient.GenerateContentAsync(systemPrompt, userPrompt, ct);
            if (geminiResult.Success && !string.IsNullOrWhiteSpace(geminiResult.Text))
            {
                extractedAdvice = TryParseGeminiResponse(geminiResult.Text)
                                  ?? GenerateDeterministicFallback(readinessData, readinessWarnings);
            }
            else
            {
                _logger.LogInformation("Gemini API not available ({Reason}). Utilizing deterministic rule engine fallback.",
                    geminiResult.ErrorMessage ?? "Not configured / Offline");
                extractedAdvice = GenerateDeterministicFallback(readinessData, readinessWarnings);
            }

            // ---------------------------------------------------------------------------------
            // STEP 4: Post-Extraction Validation Guardrails
            // ---------------------------------------------------------------------------------
            var (isAdviceValid, adviceErrors) = _validator.ValidateExtractedAdvice(extractedAdvice, readinessData);
            if (!isAdviceValid)
            {
                _logger.LogWarning("LLM advice violated deterministic constraints ({Errors}). Re-applying fallback safety rules.",
                    string.Join("; ", adviceErrors));
                extractedAdvice = GenerateDeterministicFallback(readinessData, readinessWarnings);
            }

            // ---------------------------------------------------------------------------------
            // STEP 5: Generate Formal Executive Decision Memo via allow-listed tool
            // ---------------------------------------------------------------------------------
            var briefInput = new GenerateExecutiveBriefInput
            {
                WorkflowId = context.WorkflowId,
                ReadinessData = readinessData,
                ExtractedAdvice = extractedAdvice
            };

            var briefToolResult = await _toolRegistry.ExecuteToolAsync(
                "GenerateExecutiveBriefTool",
                briefInput,
                toolCtx,
                ct);

            toolMetadata.Add("GenerateExecutiveBriefTool");

            if (!briefToolResult.Success || briefToolResult.Data is not DecisionSupportOutput decisionBrief)
            {
                var briefError = briefToolResult.ErrorMessage ?? "Failed to compile executive decision brief.";
                return new AgentResult
                {
                    Status = AgentExecutionStatus.FAILED,
                    ErrorMessages = new List<string> { briefError },
                    ExecutionSummary = briefError
                };
            }

            // ---------------------------------------------------------------------------------
            // STEP 6: Persist Immutable AIAgentExecution Audit Entry
            // ---------------------------------------------------------------------------------
            var recordInput = new RecordAiAgentExecutionInput
            {
                WorkflowId = context.WorkflowId,
                AgentName = AgentName,
                ExecutionOrder = 1,
                ExecutionStatus = "COMPLETED",
                InputSummary = $"ProcurementRequest: {readinessData.RequestNumber} (${readinessData.EstimatedTotal:N2}), TopVendor: {readinessData.TopVendorName} (${readinessData.TopQuotedPrice:N2})",
                OutputSummary = $"Action: {decisionBrief.RecommendedAction}, Confidence: {decisionBrief.ConfidenceScore:N1}%, Brief: {decisionBrief.ExecutiveBrief}",
                ValidationResult = readinessWarnings.Any() ? $"PASSED_WITH_WARNINGS: {string.Join("; ", readinessWarnings)}" : "PASSED",
                ToolExecutionMetadata = JsonSerializer.Serialize(toolMetadata, JsonOptions),
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow
            };

            await _toolRegistry.ExecuteToolAsync(
                "RecordAiAgentExecutionTool",
                recordInput,
                toolCtx,
                ct);

            toolMetadata.Add("RecordAiAgentExecutionTool");

            // ---------------------------------------------------------------------------------
            // STEP 7: Finalize Agent Result
            // ---------------------------------------------------------------------------------
            context.AddAudit(
                Stage,
                AgentName,
                "DECISION_SUPPORT_GENERATED",
                "COMPLETED",
                $"Decision memo formulated. Recommendation: {decisionBrief.RecommendedAction} for {decisionBrief.TopVendorName} (${decisionBrief.RecommendedAmount:N2}).");

            return new AgentResult
            {
                Status = AgentExecutionStatus.COMPLETED,
                ExecutionSummary = decisionBrief.ExecutiveBrief,
                ProcurementRequestId = readinessData.ProcurementRequestId,
                RequestNumber = readinessData.RequestNumber,
                EstimatedTotal = readinessData.EstimatedTotal,
                OutputData = decisionBrief
            };
        }

        private static string BuildSystemPrompt()
        {
            return @"You are the Procurement Decision Support Agent for the enterprise platform PROCURA.
Your role is to assist the human MANAGER by reviewing procurement request parameters, candidate vendor scoring, and cost bounds to formulate an executive decision memo.

CRITICAL ENTERPRISE POLICY RULES:
1. You provide DECISION SUPPORT only. You do not make binding approvals.
2. If the candidate vendor is compliant and within budget, recommend APPROVE.
3. If the candidate exceeds budget significantly or delivery SLA is unacceptable, recommend REQUEST_REVISION or REJECT with clear justification.
4. If the candidate is non-compliant, you MUST recommend REJECT or REQUEST_REVISION.
5. Return strictly valid JSON with no markdown wrapping and no backticks.

Expected JSON Schema:
{
  ""recommendedAction"": ""APPROVE"" | ""REJECT"" | ""REQUEST_REVISION"",
  ""confidenceScore"": 0.0 - 100.0,
  ""executiveSummary"": ""Concise 2-3 sentence executive recommendation for the Manager"",
  ""budgetAssessment"": ""Analysis of quoted price vs estimated budget ceiling"",
  ""riskFactors"": [ ""Risk factor 1"", ""Risk factor 2"" ],
  ""keyTradeoffs"": [ ""Tradeoff 1"", ""Tradeoff 2"" ],
  ""conditionsOrStipulations"": [ ""Condition 1"", ""Condition 2"" ]
}";
        }

        private static string BuildUserPrompt(ApprovalReadinessData r, List<string> warnings, string objective)
        {
            return $@"Review the following procurement details:
- Request Number: {r.RequestNumber}
- Title: {r.Title}
- Objective: {objective}
- Estimated Total Budget: ${r.EstimatedTotal:N2}
- Total Items: {r.ItemCount}
- Top Recommended Vendor: {r.TopVendorName} (ID: {r.TopVendorId})
- Quoted Price: ${r.TopQuotedPrice ?? 0:N2}
- Estimated Delivery Days: {r.TopDeliveryDays ?? 0} days
- Multi-Criteria Overall Score: {r.TopScore ?? 0:N1}/100
- Compliance Status: {(r.IsComplianceApproved ? "APPROVED" : "NON_COMPLIANT")}
- Vendor Evaluation Reasoning: {r.Component3Summary}
- Deterministic Warnings: {(warnings.Any() ? string.Join(", ", warnings) : "None")}";
        }

        private static ExtractedDecisionSupportInput? TryParseGeminiResponse(string rawText)
        {
            try
            {
                var clean = rawText.Trim();
                if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                    clean = clean.Substring(7);
                else if (clean.StartsWith("```", StringComparison.OrdinalIgnoreCase))
                    clean = clean.Substring(3);
                if (clean.EndsWith("```", StringComparison.OrdinalIgnoreCase))
                    clean = clean.Substring(0, clean.Length - 3);
                clean = clean.Trim();

                return JsonSerializer.Deserialize<ExtractedDecisionSupportInput>(clean, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static ExtractedDecisionSupportInput GenerateDeterministicFallback(
            ApprovalReadinessData r,
            List<string> warnings)
        {
            var isWithinBudget = !r.TopQuotedPrice.HasValue || r.TopQuotedPrice.Value <= r.EstimatedTotal;
            var isCompliant = r.IsComplianceApproved;

            string action;
            decimal confidence;
            string summary;

            if (!isCompliant)
            {
                action = "REJECT";
                confidence = 98.0m;
                summary = $"Rejection recommended: Top candidate vendor '{r.TopVendorName ?? "Candidate"}' has not achieved required compliance approvals.";
            }
            else if (!isWithinBudget && r.TopQuotedPrice.HasValue && r.TopQuotedPrice.Value > r.EstimatedTotal * 1.25m)
            {
                action = "REQUEST_REVISION";
                confidence = 92.0m;
                summary = $"Revision requested: Quoted price (${r.TopQuotedPrice.Value:N2}) exceeds estimated budget (${r.EstimatedTotal:N2}) by over 25%. Requester should renegotiate or adjust quantities.";
            }
            else
            {
                action = "APPROVE";
                confidence = isWithinBudget ? 95.0m : 85.0m;
                summary = $"Approval recommended: Vendor '{r.TopVendorName ?? "Candidate"}' achieved highest score ({r.TopScore ?? 0:N1}/100) with quoted turnaround of {r.TopDeliveryDays ?? 0} days at ${r.TopQuotedPrice ?? r.EstimatedTotal:N2}.";
            }

            var risks = new List<string>(warnings);
            if (!isWithinBudget && r.TopQuotedPrice.HasValue)
            {
                risks.Add($"Budget overrun of ${r.TopQuotedPrice.Value - r.EstimatedTotal:N2}.");
            }

            return new ExtractedDecisionSupportInput
            {
                RecommendedAction = action,
                ConfidenceScore = confidence,
                ExecutiveSummary = summary,
                BudgetAssessment = isWithinBudget
                    ? "Quoted amount is fully within authorized procurement budget."
                    : $"Quoted amount exceeds initial estimate by ${r.TopQuotedPrice!.Value - r.EstimatedTotal:N2}.",
                RiskFactors = risks,
                KeyTradeoffs = new List<string> { "Optimized delivery timeline vs unit pricing" },
                ConditionsOrStipulations = isWithinBudget
                    ? new List<string> { "Standard receipt inspection upon arrival" }
                    : new List<string> { "Managerial sign-off on budget variance" }
            };
        }
    }
}
