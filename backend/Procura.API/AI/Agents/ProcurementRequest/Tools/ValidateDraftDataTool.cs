using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;

namespace Procura.API.AI.Agents.ProcurementRequest.Tools
{
    public class ValidateDraftDataTool : IAgentTool
    {
        private readonly ProcurementRequestDeterministicValidator _validator;

        public string Name => "ValidateDraftData";
        public string Description => "Validates extracted procurement draft data against business rules without making database changes.";
        public bool IsMutating => false;

        public ValidateDraftDataTool(ProcurementRequestDeterministicValidator validator)
        {
            _validator = validator;
        }

        public Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            if (input is not ExtractedProcurementData data)
            {
                return Task.FromResult(ToolResult.Fail(Name, "Invalid tool input: expected ExtractedProcurementData."));
            }

            var validation = _validator.Validate(data);
            return Task.FromResult(ToolResult.Ok(Name, validation));
        }
    }
}
