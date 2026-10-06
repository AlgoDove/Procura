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

            // If no match on full category phrase, try individual significant words (e.g., "IT Hardware" -> "IT")
            if (vendors.Count == 0)
            {
                var tokens = category.Split(new[] { ' ', '-', '/', ',', '&' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    if (token.Length >= 2)
                    {
                        var tokenMatches = await _vendorService.GetAllAsync(status: "ACTIVE", category: token);
                        if (tokenMatches.Count > 0)
                        {
                            vendors = tokenMatches;
                            break;
                        }
                    }
                }
            }

            // If still no candidates, check if any active vendor's category is contained in the requested category text
            if (vendors.Count == 0)
            {
                var allActive = await _vendorService.GetAllAsync(status: "ACTIVE", category: null);
                var searchLower = category.Trim().ToLower();
                var matched = allActive
                    .Where(v => !string.IsNullOrWhiteSpace(v.Category) && searchLower.Contains(v.Category.Trim().ToLower()))
                    .ToList();

                if (matched.Count > 0)
                {
                    vendors = matched;
                }
            }

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