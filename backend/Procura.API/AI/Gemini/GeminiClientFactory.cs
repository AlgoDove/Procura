using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Procura.API.AI.Gemini
{
    public class GeminiClientFactory : IGeminiClientFactory
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<GeminiOptions> _options;
        private readonly ILoggerFactory _loggerFactory;

        public GeminiClientFactory(HttpClient httpClient, IOptions<GeminiOptions> options, ILoggerFactory loggerFactory)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public IGeminiClient CreateClient(GeminiAgentRole role)
        {
            var baseOptions = _options.Value;
            var roleKey = baseOptions.GetApiKeyForRole(role);

            var roleOptions = new GeminiOptions
            {
                Model = baseOptions.Model,
                TimeoutSeconds = baseOptions.TimeoutSeconds,
                MaxRetries = baseOptions.MaxRetries,
                ApiKey = roleKey,
                OrchestratorApiKey = baseOptions.OrchestratorApiKey,
                ProcurementRequestApiKey = baseOptions.ProcurementRequestApiKey,
                VendorManagementApiKey = baseOptions.VendorManagementApiKey,
                VendorEvaluationApiKey = baseOptions.VendorEvaluationApiKey,
                ApprovalWorkflowApiKey = baseOptions.ApprovalWorkflowApiKey
            };

            var roleOptionsWrapper = Microsoft.Extensions.Options.Options.Create(roleOptions);
            var logger = _loggerFactory.CreateLogger<GeminiClient>();

            return new GeminiClient(_httpClient, roleOptionsWrapper, logger, role.ToString());
        }
    }
}
