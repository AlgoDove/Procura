using System;
using System.Threading;
using System.Threading.Tasks;

namespace Procura.API.AI.Core
{
    public class ToolExecutionContext
    {
        public Guid WorkflowId { get; set; }
        public Guid RequesterId { get; set; }
        public string RequesterRole { get; set; } = "EMPLOYEE";
    }

    public class ToolResult
    {
        public bool Success { get; set; }
        public string ToolName { get; set; } = string.Empty;
        public object? Data { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsSecurityViolation { get; set; } = false;

        public static ToolResult Ok(string toolName, object? data) =>
            new ToolResult { Success = true, ToolName = toolName, Data = data };

        public static ToolResult Fail(string toolName, string error, bool isSecurityViolation = false) =>
            new ToolResult { Success = false, ToolName = toolName, ErrorMessage = error, IsSecurityViolation = isSecurityViolation };
    }

    public interface IAgentTool
    {
        string Name { get; }
        string Description { get; }
        bool IsMutating { get; }
        Task<ToolResult> ExecuteAsync(object input, ToolExecutionContext context, CancellationToken ct = default);
    }
}
