using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;
using Procura.API.Modules.ProcurementRequest.DTOs;
using Procura.API.Modules.ProcurementRequest.Services;

namespace Procura.API.AI.Agents.ProcurementRequest.Tools
{
    public class CreateDraftRequestTool : IAgentTool
    {
        private readonly IProcurementRequestService _service;

        public string Name => "CreateDraftRequest";
        public string Description => "Creates a new procurement request in DRAFT status using the authenticated requester identity.";
        public bool IsMutating => true;

        public CreateDraftRequestTool(IProcurementRequestService service)
        {
            _service = service;
        }

        public async Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            CreateProcurementRequestDto dto;

            if (input is CreateProcurementRequestDto directDto)
            {
                dto = directDto;
            }
            else if (input is ExtractedProcurementData extracted)
            {
                dto = new CreateProcurementRequestDto
                {
                    Title = extracted.Title,
                    Description = extracted.Description,
                    Justification = extracted.Justification,
                    Priority = extracted.Priority,
                    RequiredByDate = extracted.RequiredByDate ?? DateTime.UtcNow.AddDays(14),
                    Items = extracted.Items.Select(i => new CreateProcurementRequestItemDto
                    {
                        ItemName = i.ItemName,
                        Description = i.Description,
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        EstimatedUnitPrice = i.EstimatedUnitPrice
                    }).ToList()
                };
            }
            else
            {
                return ToolResult.Fail(Name, "Invalid tool input: expected CreateProcurementRequestDto or ExtractedProcurementData.");
            }

            try
            {
                var result = await _service.CreateAsync(dto, context.RequesterId);

                // Tool-Result Validation: Validate invariants
                if (result == null)
                {
                    return ToolResult.Fail(Name, "Tool result validation failed: service returned null.");
                }

                if (result.Id == Guid.Empty)
                {
                    return ToolResult.Fail(Name, "Tool result validation failed: request ID is empty.");
                }

                if (string.IsNullOrWhiteSpace(result.RequestNumber))
                {
                    return ToolResult.Fail(Name, "Tool result validation failed: request number is missing.");
                }

                if (result.Status != "DRAFT")
                {
                    return ToolResult.Fail(Name, $"Tool result validation failed: status '{result.Status}' is not DRAFT.", isSecurityViolation: true);
                }

                if (result.RequesterId != context.RequesterId)
                {
                    return ToolResult.Fail(Name, "Tool result validation failed: requester ID mismatch.", isSecurityViolation: true);
                }

                if (result.EstimatedTotal < 0)
                {
                    return ToolResult.Fail(Name, "Tool result validation failed: estimated total is negative.");
                }

                return ToolResult.Ok(Name, result);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail(Name, $"Failed to create draft request: {ex.Message}");
            }
        }
    }
}
