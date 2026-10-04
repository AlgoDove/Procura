using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;
using Procura.API.Modules.VendorEvaluation.Services;

namespace Procura.API.AI.Agents.VendorEvaluation.Tools
{
    public class GenerateRecommendationTool : IAgentTool
    {
        private readonly IVendorEvaluationService _evaluationService;

        public string Name => "GenerateRecommendationTool";
        public string Description => "Retrieves top-ranked vendor recommendation summary and evaluation scores for a procurement request.";
        public bool IsMutating => false;

        public GenerateRecommendationTool(IVendorEvaluationService evaluationService)
        {
            _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        }

        public async Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            try
            {
                Guid procurementRequestId = Guid.Empty;

                if (input is Guid id)
                {
                    procurementRequestId = id;
                }
                else if (input is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var parsedId))
                    {
                        procurementRequestId = parsedId;
                    }
                    else if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("ProcurementRequestId", out var prop) && Guid.TryParse(prop.GetString(), out var propId))
                    {
                        procurementRequestId = propId;
                    }
                }
                else if (input != null)
                {
                    if (Guid.TryParse(input.ToString(), out var parsedStrId))
                    {
                        procurementRequestId = parsedStrId;
                    }
                }

                if (procurementRequestId == Guid.Empty)
                {
                    return ToolResult.Fail(Name, "Invalid GenerateRecommendationTool input: ProcurementRequestId is required.");
                }

                var summary = await _evaluationService.GetRecommendationSummaryAsync(procurementRequestId, ct);
                if (summary == null)
                {
                    return ToolResult.Fail(Name, $"No evaluation recommendation summary found for ProcurementRequestId {procurementRequestId}.");
                }

                return ToolResult.Ok(Name, summary);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail(Name, $"GenerateRecommendationTool execution failed: {ex.Message}");
            }
        }
    }
}
