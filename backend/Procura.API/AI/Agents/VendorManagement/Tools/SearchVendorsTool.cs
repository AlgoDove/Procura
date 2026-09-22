using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;
using Procura.API.Modules.VendorManagement.Services;

namespace Procura.API.AI.Agents.VendorManagement.Tools
{
    public class SearchVendorsTool : IAgentTool
    {
        private readonly IVendorService _vendorService;

        public string Name => "SearchVendors";
        public string Description => "Searches ACTIVE vendors by category. Read-only, no database writes.";
        public bool IsMutating => false;

        public SearchVendorsTool(IVendorService vendorService)
        {
            _vendorService = vendorService;
        }

        public async Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            if (input is not string category || string.IsNullOrWhiteSpace(category))
            {
                return ToolResult.Fail(Name, "Invalid tool input: expected a non-empty category string.");
            }

            var vendors = await _vendorService.GetAllAsync(status: "ACTIVE", category: category);

            var candidates = vendors
                .Select(v => new VendorCandidate
                {
                    VendorId = v.Id,
                    Name = v.Name,
                    Rating = v.Rating
                })
                .ToList();

            return ToolResult.Ok(Name, candidates);
        }
    }
}