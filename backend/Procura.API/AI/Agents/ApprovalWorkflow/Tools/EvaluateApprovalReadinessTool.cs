using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;
using Procura.API.Modules.ProcurementRequest.Repositories;
using Procura.API.Modules.VendorEvaluation.Repositories;
using Procura.API.Modules.VendorManagement.Repositories;

namespace Procura.API.AI.Agents.ApprovalWorkflow.Tools
{
    public class EvaluateApprovalReadinessTool : IAgentTool
    {
        private readonly IProcurementRequestRepository _requestRepository;
        private readonly IVendorQuoteRepository _quoteRepository;
        private readonly IVendorEvaluationRepository _evaluationRepository;
        private readonly IVendorRepository _vendorRepository;

        public string Name => "EvaluateApprovalReadinessTool";
        public string Description => "Gathers procurement request specifications, item quantities, candidate vendor quotes, and component 3 evaluation scores to assess readiness for manager review.";
        public bool IsMutating => false;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public EvaluateApprovalReadinessTool(
            IProcurementRequestRepository requestRepository,
            IVendorQuoteRepository quoteRepository,
            IVendorEvaluationRepository evaluationRepository,
            IVendorRepository vendorRepository)
        {
            _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
            _quoteRepository = quoteRepository ?? throw new ArgumentNullException(nameof(quoteRepository));
            _evaluationRepository = evaluationRepository ?? throw new ArgumentNullException(nameof(evaluationRepository));
            _vendorRepository = vendorRepository ?? throw new ArgumentNullException(nameof(vendorRepository));
        }

        public async Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            try
            {
                Guid requestId = Guid.Empty;

                if (input is Guid g)
                {
                    requestId = g;
                }
                else if (input is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var parsed))
                        requestId = parsed;
                    else if (element.TryGetProperty("procurementRequestId", out var prop) && Guid.TryParse(prop.GetString(), out var parsedProp))
                        requestId = parsedProp;
                }
                else if (input != null)
                {
                    var json = JsonSerializer.Serialize(input, JsonOptions);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("procurementRequestId", out var prop) && Guid.TryParse(prop.GetString(), out var parsedProp))
                        requestId = parsedProp;
                }

                if (requestId == Guid.Empty)
                {
                    return ToolResult.Fail(Name, "Invalid input: Valid ProcurementRequestId is required.");
                }

                var request = await _requestRepository.GetByIdAsync(requestId);
                if (request == null)
                {
                    return ToolResult.Fail(Name, $"Procurement request {requestId} not found.");
                }

                var quotes = (await _quoteRepository.GetByProcurementRequestIdAsync(requestId, ct)).ToList();
                var evaluations = (await _evaluationRepository.GetByProcurementRequestIdAsync(requestId, ct)).OrderBy(e => e.Rank).ToList();

                var topEval = evaluations.FirstOrDefault();
                var topQuote = topEval != null ? quotes.FirstOrDefault(q => q.VendorId == topEval.VendorId) : null;
                
                string? topVendorName = null;
                if (topEval != null)
                {
                    var vendor = await _vendorRepository.GetByIdAsync(topEval.VendorId);
                    topVendorName = vendor?.Name ?? topQuote?.VendorName;
                }

                var readinessData = new ApprovalReadinessData
                {
                    ProcurementRequestId = request.Id,
                    RequestNumber = request.RequestNumber,
                    Title = request.Title,
                    EstimatedTotal = request.EstimatedTotal,
                    ItemCount = request.Items.Count,
                    TopVendorId = topEval?.VendorId,
                    TopVendorName = topVendorName,
                    TopQuotedPrice = topQuote?.QuotedPrice,
                    TopDeliveryDays = topQuote?.EstimatedDeliveryDays,
                    IsComplianceApproved = topQuote?.IsComplianceApproved ?? true,
                    TopScore = topEval?.OverallScore,
                    Component3Summary = topEval?.Reasoning,
                    HasQuotes = quotes.Any(),
                    HasEvaluations = evaluations.Any(),
                    TotalCandidatesEvaluated = evaluations.Count
                };

                return ToolResult.Ok(Name, readinessData);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail(Name, $"EvaluateApprovalReadinessTool failed: {ex.Message}");
            }
        }
    }
}
