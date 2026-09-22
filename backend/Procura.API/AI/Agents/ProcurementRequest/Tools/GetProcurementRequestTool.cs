using System;
using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;
using Procura.API.Modules.ProcurementRequest.Services;

namespace Procura.API.AI.Agents.ProcurementRequest.Tools
{
    public class GetProcurementRequestTool : IAgentTool
    {
        private readonly IProcurementRequestService _service;

        public string Name => "GetProcurementRequest";
        public string Description => "Retrieves details of an existing procurement request.";
        public bool IsMutating => false;

        public GetProcurementRequestTool(IProcurementRequestService service)
        {
            _service = service;
        }

        public async Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            Guid requestId;
            if (input is Guid id)
            {
                requestId = id;
            }
            else if (input is string idStr && Guid.TryParse(idStr, out var parsed))
            {
                requestId = parsed;
            }
            else
            {
                return ToolResult.Fail(Name, "Invalid tool input: expected Guid requestId.");
            }

            try
            {
                var request = await _service.GetByIdAsync(requestId);
                if (request == null)
                {
                    return ToolResult.Fail(Name, $"Procurement request {requestId} was not found.");
                }

                // Authorization check: if role is EMPLOYEE, must be owner
                if (context.RequesterRole == "EMPLOYEE" && request.RequesterId != context.RequesterId)
                {
                    return ToolResult.Fail(Name, "Unauthorized: requester cannot view another user's procurement request.", isSecurityViolation: true);
                }

                return ToolResult.Ok(Name, request);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail(Name, $"Error fetching request: {ex.Message}");
            }
        }
    }
}
