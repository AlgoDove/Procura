using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Services;

namespace Procura.API.AI.Agents.VendorEvaluation.Tools
{
    public class ScoreVendorsTool : IAgentTool
    {
        private readonly IVendorEvaluationService _evaluationService;

        public string Name => "ScoreVendorsTool";
        public string Description => "Runs multi-criteria evaluation and ranking algorithm on candidate vendors for a procurement request.";
        public bool IsMutating => true;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public ScoreVendorsTool(IVendorEvaluationService evaluationService)
        {
            _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        }

        public async Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            try
            {
                EvaluateVendorsRequestDto? requestDto = null;

                if (input is EvaluateVendorsRequestDto dto)
                {
                    requestDto = dto;
                }
                else if (input is JsonElement element)
                {
                    requestDto = JsonSerializer.Deserialize<EvaluateVendorsRequestDto>(element.GetRawText(), JsonOptions);
                }
                else if (input != null)
                {
                    var json = JsonSerializer.Serialize(input, JsonOptions);
                    requestDto = JsonSerializer.Deserialize<EvaluateVendorsRequestDto>(json, JsonOptions);
                }

                if (requestDto == null || requestDto.ProcurementRequestId == Guid.Empty)
                {
                    return ToolResult.Fail(Name, "Invalid ScoreVendorsTool input: ProcurementRequestId is required.");
                }

                var summary = await _evaluationService.EvaluateAndRankCandidateVendorsAsync(requestDto, ct);
                return ToolResult.Ok(Name, summary);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail(Name, $"ScoreVendorsTool execution failed: {ex.Message}");
            }
        }
    }
}
