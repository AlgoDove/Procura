using System;
using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;
using Procura.API.Modules.VendorManagement.Entities;
using Procura.API.Shared.Data;

namespace Procura.API.AI.Agents.VendorManagement.Tools
{
    public class SelectVendorToolInput
    {
        public Guid ProcurementRequestId { get; set; }
        public Guid VendorId { get; set; }
    }

    public class SelectVendorTool : IAgentTool
    {
        private readonly ApplicationDbContext _context;

        public string Name => "SelectVendor";
        public string Description => "Records a vendor as selected for a procurement request. Mutating.";
        public bool IsMutating => true;

        public SelectVendorTool(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            if (input is not SelectVendorToolInput data)
            {
                return ToolResult.Fail(Name, "Invalid tool input: expected SelectVendorToolInput.");
            }

            if (data.ProcurementRequestId == Guid.Empty)
            {
                return ToolResult.Fail(Name, "ProcurementRequestId is required and cannot be empty.");
            }

            if (data.VendorId == Guid.Empty)
            {
                return ToolResult.Fail(Name, "VendorId is required and cannot be empty.");
            }

            var selection = new VendorSelection
            {
                ProcurementRequestId = data.ProcurementRequestId,
                VendorId = data.VendorId
            };

            _context.VendorSelections.Add(selection);
            await _context.SaveChangesAsync(ct);

            return ToolResult.Ok(Name, selection);
        }
    }
}