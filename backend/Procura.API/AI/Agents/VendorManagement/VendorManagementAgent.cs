using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Procura.API.AI.Agents.VendorManagement.Tools;
using Procura.API.AI.Core;
using Procura.API.AI.Gemini;

namespace Procura.API.AI.Agents.VendorManagement
{
    public class VendorManagementAgent : IVendorManagementAgent
    {
        private readonly IGeminiClient _geminiClient;
        private readonly ToolRegistry _toolRegistry;
        private readonly VendorManagementDeterministicValidator _validator;
        private readonly ILogger<VendorManagementAgent> _logger;

        public string AgentName => "VendorManagementAgent";
        public WorkflowStage Stage => WorkflowStage.VENDOR_SELECTION;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public VendorManagementAgent(
            IGeminiClient geminiClient,
            ToolRegistry toolRegistry,
            VendorManagementDeterministicValidator validator,
            ILogger<VendorManagementAgent> logger)
        {
            _geminiClient = geminiClient;
            _toolRegistry = toolRegistry;
            _validator = validator;
            _logger = logger;
        }

        public async Task<AgentResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default)
        {
            context.AddAudit(Stage, AgentName, "AGENT_STARTED", "IN_PROGRESS", "VendorManagementAgent started execution.");

            // 0. This stage requires a completed Procurement Request stage before it.
            // We never re-derive this from the objective text - only from the orchestrator-populated context.
            if (!context.ProcurementRequestId.HasValue)
            {
                var noRequestMsg = "VENDOR_SELECTION requires a completed PROCUREMENT_REQUEST stage; no ProcurementRequestId found on the workflow context.";
                context.AddAudit(Stage, AgentName, "AGENT_FAILED", "FAILED", noRequestMsg);
                return new AgentResult
                {
                    Status = AgentExecutionStatus.FAILED,
                    ErrorMessages = new List<string> { noRequestMsg },
                    ExecutionSummary = noRequestMsg
                };
            }

            // 1. Build system prompt - Gemini extracts intent only, never selects vendors itself
            var systemPrompt = BuildSystemPrompt();

            // 2. Call Gemini
            var geminiResult = await _geminiClient.GenerateContentAsync(systemPrompt, context.Objective, ct);

            if (!geminiResult.Success)
            {
                var errorMsg = geminiResult.ErrorMessage ?? "LLM extraction failed.";
                context.AddAudit(Stage, AgentName, "AGENT_FAILED", "FAILED", errorMsg);
                return new AgentResult
                {
                    Status = AgentExecutionStatus.FAILED,
                    ErrorMessages = new List<string> { errorMsg },
                    ExecutionSummary = errorMsg
                };
            }

            // 3. Parse Gemini's JSON output
            ExtractedVendorIntent? extractedIntent;
            try
            {
                var rawText = geminiResult.Text!.Trim();
                if (rawText.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                {
                    rawText = rawText.Substring(7);
                }
                else if (rawText.StartsWith("```", StringComparison.OrdinalIgnoreCase))
                {
                    rawText = rawText.Substring(3);
                }
                if (rawText.EndsWith("```", StringComparison.OrdinalIgnoreCase))
                {
                    rawText = rawText.Substring(0, rawText.Length - 3);
                }
                rawText = rawText.Trim();

                extractedIntent = JsonSerializer.Deserialize<ExtractedVendorIntent>(rawText, JsonOptions);
                if (extractedIntent == null)
                {
                    throw new JsonException("Deserialization returned null.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse JSON response from Gemini. Raw text: {Raw}", geminiResult.Text);
                var errorMsg = "LLM returned malformed structured data.";
                context.AddAudit(Stage, AgentName, "AGENT_FAILED", "FAILED", errorMsg);
                return new AgentResult
                {
                    Status = AgentExecutionStatus.FAILED,
                    ErrorMessages = new List<string> { errorMsg },
                    ExecutionSummary = errorMsg
                };
            }

            // 4. Deterministic validation - independent of the LLM
            var validation = _validator.Validate(extractedIntent);

            if (validation.MissingFields.Count > 0)
            {
                var missingSummary = string.Join("; ", validation.MissingFields);
                var prompt = extractedIntent.ClarificationPrompt ?? $"Please clarify: {missingSummary}";
                context.AddAudit(Stage, AgentName, "VALIDATION_FAILED", "NEEDS_USER_INPUT", $"Missing vendor intent info: {missingSummary}");

                return new AgentResult
                {
                    Status = AgentExecutionStatus.NEEDS_USER_INPUT,
                    MissingFields = validation.MissingFields,
                    ClarificationPrompt = prompt,
                    ExecutionSummary = $"Information incomplete. {prompt}",
                    ProcurementRequestId = context.ProcurementRequestId,
                    RequestNumber = context.RequestNumber,
                    OutputData = extractedIntent
                };
            }

            if (validation.Errors.Count > 0)
            {
                var errorSummary = string.Join("; ", validation.Errors);
                context.AddAudit(Stage, AgentName, "VALIDATION_FAILED", "FAILED", $"Deterministic validation failed: {errorSummary}");

                return new AgentResult
                {
                    Status = AgentExecutionStatus.FAILED,
                    ValidationErrors = validation.Errors,
                    ExecutionSummary = $"Validation rejected extracted data: {errorSummary}",
                    ProcurementRequestId = context.ProcurementRequestId,
                    RequestNumber = context.RequestNumber,
                    OutputData = extractedIntent
                };
            }

            context.AddAudit(Stage, AgentName, "VALIDATION_PASSED", "SUCCESS", "Extracted vendor intent passed deterministic validation.");

            // 5. Call SearchVendors tool (read-only) - real database lookup, not invented by Gemini
            var toolContext = new ToolExecutionContext
            {
                WorkflowId = context.WorkflowId,
                RequesterId = context.RequesterId,
                RequesterRole = context.RequesterRole
            };

            context.AddAudit(Stage, AgentName, "TOOL_REQUESTED", "PENDING", "Requesting SearchVendors tool.", toolName: "SearchVendors");
            var searchResult = await _toolRegistry.ExecuteToolAsync("SearchVendors", extractedIntent.Category, toolContext, ct);

            if (!searchResult.Success)
            {
                context.AddAudit(Stage, AgentName, searchResult.IsSecurityViolation ? "TOOL_BLOCKED" : "TOOL_FAILED", "FAILED", searchResult.ErrorMessage ?? "SearchVendors failed.", toolName: "SearchVendors", isSecurityViolation: searchResult.IsSecurityViolation);
                return new AgentResult
                {
                    Status = AgentExecutionStatus.FAILED,
                    ErrorMessages = new List<string> { searchResult.ErrorMessage ?? "Failed to search vendors." },
                    ProcurementRequestId = context.ProcurementRequestId,
                    RequestNumber = context.RequestNumber
                };
            }

            context.AddAudit(Stage, AgentName, "TOOL_COMPLETED", "SUCCESS", "SearchVendors executed successfully.", toolName: "SearchVendors");
            var candidates = (List<VendorCandidate>)searchResult.Data!;

            // 6. Zero candidates is a valid, informative outcome - not a failure
            if (candidates.Count == 0)
            {
                var noneSummary = $"No active vendors found in category '{extractedIntent.Category}'. Consider adding a vendor before continuing.";
                context.AddAudit(Stage, AgentName, "AGENT_COMPLETED", "COMPLETED", noneSummary);

                return new AgentResult
                {
                    Status = AgentExecutionStatus.COMPLETED,
                    ExecutionSummary = noneSummary,
                    ProcurementRequestId = context.ProcurementRequestId,
                    RequestNumber = context.RequestNumber,
                    OutputData = new VendorSelectionOutput { Candidates = candidates }
                };
            }

            // 7. Deterministically pick the best candidate - never Gemini's decision
            var chosen = candidates.OrderByDescending(c => c.Rating).First();

            // 8. Call SelectVendor tool (mutating)
            var selectInput = new SelectVendorToolInput
            {
                ProcurementRequestId = context.ProcurementRequestId.Value,
                VendorId = chosen.VendorId
            };

            context.AddAudit(Stage, AgentName, "TOOL_REQUESTED", "PENDING", $"Requesting SelectVendor for vendor {chosen.VendorId}.", toolName: "SelectVendor");
            var selectResult = await _toolRegistry.ExecuteToolAsync("SelectVendor", selectInput, toolContext, ct);

            if (!selectResult.Success)
            {
                context.AddAudit(Stage, AgentName, selectResult.IsSecurityViolation ? "TOOL_BLOCKED" : "TOOL_FAILED", "FAILED", selectResult.ErrorMessage ?? "SelectVendor failed.", toolName: "SelectVendor", isSecurityViolation: selectResult.IsSecurityViolation);
                return new AgentResult
                {
                    Status = AgentExecutionStatus.FAILED,
                    ErrorMessages = new List<string> { selectResult.ErrorMessage ?? "Failed to select vendor." },
                    ProcurementRequestId = context.ProcurementRequestId,
                    RequestNumber = context.RequestNumber
                };
            }

            context.AddAudit(Stage, AgentName, "TOOL_COMPLETED", "SUCCESS", "SelectVendor executed successfully.", toolName: "SelectVendor");

            var summary = $"Found {candidates.Count} candidate vendor(s) in '{extractedIntent.Category}'; selected {chosen.Name} (rating {chosen.Rating}) for {context.RequestNumber}.";
            context.AddAudit(Stage, AgentName, "AGENT_COMPLETED", "COMPLETED", summary);

            return new AgentResult
            {
                Status = AgentExecutionStatus.COMPLETED,
                ExecutionSummary = summary,
                ProcurementRequestId = context.ProcurementRequestId,
                RequestNumber = context.RequestNumber,
                OutputData = new VendorSelectionOutput
                {
                    Candidates = candidates,
                    SelectedVendorId = chosen.VendorId,
                    SelectedVendorName = chosen.Name
                }
            };
        }

        private static string BuildSystemPrompt()
        {
            return @"You are the Procura Vendor Intelligence Specialist Agent.
Your SOLE responsibility is to read a natural language objective about finding vendors for a
procurement request, and extract ONLY the following structured JSON:

{
  ""category"": ""<the type/category of vendor or goods being requested, e.g. 'Office Supplies'>"",
  ""mentionedRequestNumber"": ""<a procurement request number if explicitly mentioned in the text, else null>"",
  ""hasSufficientInformation"": true/false,
  ""missingInformationReasons"": [""<reason if information is insufficient>""],
  ""clarificationPrompt"": ""<a question to ask the user if information is insufficient, else null>""
}

CRITICAL RULES:
- You do NOT select, invent, rank, or recommend any actual vendors. You only extract the category.
- You do NOT decide which procurement request this relates to - that identity comes from the system,
  not from your output. mentionedRequestNumber is for logging/reference only.
- If no clear category can be determined from the text, set hasSufficientInformation to false and
  provide a clarificationPrompt asking the user what type of vendor they need.
- Return ONLY valid JSON, no markdown formatting, no explanation text.";
        }
    }
}