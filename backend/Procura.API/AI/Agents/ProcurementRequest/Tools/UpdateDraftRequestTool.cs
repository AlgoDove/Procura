using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;
using Procura.API.Modules.ProcurementRequest.DTOs;
using Procura.API.Modules.ProcurementRequest.Services;

namespace Procura.API.AI.Agents.ProcurementRequest.Tools
{
    public class UpdateDraftToolInput
    {
        public Guid RequestId { get; set; }
        public UpdateProcurementRequestDto Dto { get; set; } = null!;
    }

    public class UpdateDraftRequestTool : IAgentTool
    {
        private readonly IProcurementRequestService _service;

        public string Name => "UpdateDraftRequest";
        public string Description => "Updates an existing DRAFT procurement request. Cannot modify non-DRAFT requests.";
        public bool IsMutating => true;

        public UpdateDraftRequestTool(IProcurementRequestService service)
        {
            _service = service;
        }

        public async Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            UpdateDraftToolInput toolInput;

            if (input is UpdateDraftToolInput direct)
            {
                toolInput = direct;
            }
            else
            {
                return ToolResult.Fail(Name, "Invalid tool input: expected UpdateDraftToolInput.");
            }

            try
            {
                // Verify request exists, ownership, and DRAFT status prior to mutating
                var existing = await _service.GetByIdAsync(toolInput.RequestId);
                if (existing == null)
                {
                    return ToolResult.Fail(Name, $"Procurement request {toolInput.RequestId} not found.");
                }

                if (existing.RequesterId != context.RequesterId)
                {
                    return ToolResult.Fail(Name, "Unauthorized: caller cannot update another user's request.", isSecurityViolation: true);
                }

                if (existing.Status != "DRAFT")
                {
                    return ToolResult.Fail(Name, $"Invalid operation: Request {toolInput.RequestId} is in status '{existing.Status}'. Only DRAFT requests can be updated by the agent.", isSecurityViolation: true);
                }

                var result = await _service.UpdateAsync(toolInput.RequestId, toolInput.Dto, context.RequesterId);

                // Tool-Result Validation
                if (result == null)
                {
                    return ToolResult.Fail(Name, "Tool result validation failed: update returned null.");
                }

                if (result.Status != "DRAFT")
                {
                    return ToolResult.Fail(Name, "Tool result validation failed: status is not DRAFT.", isSecurityViolation: true);
                }

                if (result.RequesterId != context.RequesterId)
                {
                    return ToolResult.Fail(Name, "Tool result validation failed: requester mismatch.", isSecurityViolation: true);
                }

                if (result.EstimatedTotal < 0)
                {
                    return ToolResult.Fail(Name, "Tool result validation failed: estimated total cannot be negative.");
                }

                return ToolResult.Ok(Name, result);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail(Name, $"Failed to update draft request: {ex.Message}");
            }
        }
    }
}
