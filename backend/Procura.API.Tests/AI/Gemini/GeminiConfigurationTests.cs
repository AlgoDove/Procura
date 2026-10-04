using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Procura.API.AI.Gemini;
using Xunit;

namespace Procura.API.Tests.AI.Gemini
{
    public class GeminiConfigurationTests
    {
        [Fact]
        public void GetApiKeyForRole_ReturnsRoleSpecificKey_WhenConfigured()
        {
            var options = new GeminiOptions
            {
                ApiKey = "global-fallback-key",
                OrchestratorApiKey = "orchestrator-key",
                VendorManagementApiKey = "vendor-mgmt-key",
                VendorEvaluationApiKey = "vendor-eval-key",
                ApprovalWorkflowApiKey = "approval-workflow-key"
            };

            Assert.Equal("orchestrator-key", options.GetApiKeyForRole(GeminiAgentRole.ProcurementRequest));
            Assert.Equal("vendor-mgmt-key", options.GetApiKeyForRole(GeminiAgentRole.VendorManagement));
            Assert.Equal("vendor-eval-key", options.GetApiKeyForRole(GeminiAgentRole.VendorEvaluation));
            Assert.Equal("approval-workflow-key", options.GetApiKeyForRole(GeminiAgentRole.ApprovalWorkflow));
        }

        [Fact]
        public void GetApiKeyForRole_FallsBackToGlobalApiKey_WhenRoleKeyNotConfigured()
        {
            var options = new GeminiOptions
            {
                ApiKey = "global-fallback-key"
            };

            Assert.Equal("global-fallback-key", options.GetApiKeyForRole(GeminiAgentRole.ProcurementRequest));
            Assert.Equal("global-fallback-key", options.GetApiKeyForRole(GeminiAgentRole.VendorManagement));
            Assert.Equal("global-fallback-key", options.GetApiKeyForRole(GeminiAgentRole.VendorEvaluation));
            Assert.Equal("global-fallback-key", options.GetApiKeyForRole(GeminiAgentRole.ApprovalWorkflow));
        }

        [Fact]
        public void GetApiKeyForRole_ReturnsNull_WhenNeitherRoleNorGlobalKeyConfigured()
        {
            var options = new GeminiOptions();

            Assert.Null(options.GetApiKeyForRole(GeminiAgentRole.ProcurementRequest));
            Assert.Null(options.GetApiKeyForRole(GeminiAgentRole.VendorManagement));
            Assert.Null(options.GetApiKeyForRole(GeminiAgentRole.VendorEvaluation));
            Assert.Null(options.GetApiKeyForRole(GeminiAgentRole.ApprovalWorkflow));
        }

        [Fact]
        public void GeminiClientFactory_CreatesClientForEachRole()
        {
            var options = Options.Create(new GeminiOptions
            {
                Model = "gemini-3.5-flash",
                ApiKey = "global-fallback-key",
                OrchestratorApiKey = "key-1",
                VendorManagementApiKey = "key-2",
                VendorEvaluationApiKey = "key-3",
                ApprovalWorkflowApiKey = "key-4"
            });

            using var httpClient = new HttpClient();
            var factory = new GeminiClientFactory(httpClient, options, NullLoggerFactory.Instance);

            var client1 = factory.CreateClient(GeminiAgentRole.ProcurementRequest);
            var client2 = factory.CreateClient(GeminiAgentRole.VendorManagement);
            var client3 = factory.CreateClient(GeminiAgentRole.VendorEvaluation);
            var client4 = factory.CreateClient(GeminiAgentRole.ApprovalWorkflow);

            Assert.NotNull(client1);
            Assert.NotNull(client2);
            Assert.NotNull(client3);
            Assert.NotNull(client4);
        }

        [Fact]
        public async Task GeminiClient_ReturnsAuthErrorWithRoleHint_WhenKeyIsMissing()
        {
            var options = Options.Create(new GeminiOptions
            {
                Model = "gemini-3.5-flash"
            });

            using var httpClient = new HttpClient();
            var factory = new GeminiClientFactory(httpClient, options, NullLoggerFactory.Instance);
            var client = factory.CreateClient(GeminiAgentRole.VendorManagement);

            var result = await client.GenerateContentAsync("system", "user");

            Assert.False(result.Success);
            Assert.True(result.IsAuthError);
            Assert.Contains("VendorManagement", result.ErrorMessage);
            Assert.Contains("Gemini:VendorManagementApiKey", result.ErrorMessage);
        }
    }
}
