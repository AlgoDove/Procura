using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Procura.API.AI.Core
{
    public class ToolRegistry
    {
        private readonly Dictionary<string, IAgentTool> _tools = new(StringComparer.OrdinalIgnoreCase);

        public ToolRegistry(IEnumerable<IAgentTool> tools)
        {
            foreach (var tool in tools)
            {
                _tools[tool.Name] = tool;
            }
        }

        public bool IsAllowed(string toolName) => _tools.ContainsKey(toolName);

        public IAgentTool? GetTool(string toolName)
        {
            _tools.TryGetValue(toolName, out var tool);
            return tool;
        }

        public async Task<ToolResult> ExecuteToolAsync(string toolName, object input, ToolExecutionContext context, CancellationToken ct = default)
        {
            if (!IsAllowed(toolName))
            {
                return ToolResult.Fail(
                    toolName,
                    $"Tool '{toolName}' is blocked: not permitted in the allow-listed tool registry.",
                    isSecurityViolation: true);
            }

            var tool = _tools[toolName];
            return await tool.ExecuteAsync(input, context, ct);
        }
    }
}
