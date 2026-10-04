namespace Procura.API.AI.Gemini
{
    public class GeminiOptions
    {
        public string? ApiKey { get; set; }
        public string? OrchestratorApiKey { get; set; }
        public string? ProcurementRequestApiKey { get; set; }
        public string? VendorManagementApiKey { get; set; }
        public string? VendorEvaluationApiKey { get; set; }
        public string? ApprovalWorkflowApiKey { get; set; }

        public string? Model { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 2;

        /// <summary>
        /// Resolves the effective API key for a specific agent role, falling back to the global ApiKey.
        /// </summary>
        public string? GetApiKeyForRole(GeminiAgentRole role)
        {
            var key = role switch
            {
                GeminiAgentRole.ProcurementRequest => !string.IsNullOrWhiteSpace(OrchestratorApiKey) 
                    ? OrchestratorApiKey 
                    : (!string.IsNullOrWhiteSpace(ProcurementRequestApiKey) ? ProcurementRequestApiKey : ApiKey),
                GeminiAgentRole.VendorManagement => !string.IsNullOrWhiteSpace(VendorManagementApiKey) 
                    ? VendorManagementApiKey 
                    : ApiKey,
                GeminiAgentRole.VendorEvaluation => !string.IsNullOrWhiteSpace(VendorEvaluationApiKey) 
                    ? VendorEvaluationApiKey 
                    : ApiKey,
                GeminiAgentRole.ApprovalWorkflow => !string.IsNullOrWhiteSpace(ApprovalWorkflowApiKey) 
                    ? ApprovalWorkflowApiKey 
                    : ApiKey,
                _ => ApiKey
            };
            return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        }
    }

    public enum GeminiAgentRole
    {
        ProcurementRequest,
        VendorManagement,
        VendorEvaluation,
        ApprovalWorkflow
    }
}
