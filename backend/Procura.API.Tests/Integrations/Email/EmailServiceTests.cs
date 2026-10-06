using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Procura.API.Integrations.Email;
using Xunit;

namespace Procura.API.Tests.Integrations.Email
{
    public class EmailServiceTests
    {
        [Fact]
        public void EmailTemplates_BuildSubject_Approved_ReturnsExpectedSubject()
        {
            var subject = EmailTemplates.BuildSubject("APPROVED", "PR-1024");
            Assert.Equal("Procurement Request Approved — PR-1024", subject);
        }

        [Fact]
        public void EmailTemplates_BuildSubject_Rejected_ReturnsExpectedSubject()
        {
            var subject = EmailTemplates.BuildSubject("REJECTED", "PR-1024");
            Assert.Equal("Procurement Request Rejected — PR-1024", subject);
        }

        [Fact]
        public void EmailTemplates_BuildPlainTextBody_Approved_DoesNotContainRejectionReason()
        {
            var body = EmailTemplates.BuildPlainTextBody(
                "Jane Doe",
                "PR-1024",
                "Office Laptop Procurement",
                "APPROVED",
                null,
                "John Manager");

            Assert.Contains("Hello Jane Doe,", body);
            Assert.Contains("Your procurement request has been approved.", body);
            Assert.Contains("Request Number: PR-1024", body);
            Assert.Contains("Request: Office Laptop Procurement", body);
            Assert.Contains("Status: APPROVED", body);
            Assert.Contains("Approved By: John Manager", body);
            Assert.DoesNotContain("Reason:", body);
        }

        [Fact]
        public void EmailTemplates_BuildPlainTextBody_Rejected_ContainsRejectionReason()
        {
            var body = EmailTemplates.BuildPlainTextBody(
                "Jane Doe",
                "PR-1024",
                "Office Laptop Procurement",
                "REJECTED",
                "Budget exceeded for current quarter",
                "John Manager");

            Assert.Contains("Hello Jane Doe,", body);
            Assert.Contains("Your procurement request has been rejected.", body);
            Assert.Contains("Request Number: PR-1024", body);
            Assert.Contains("Request: Office Laptop Procurement", body);
            Assert.Contains("Status: REJECTED", body);
            Assert.Contains("Reviewed By: John Manager", body);
            Assert.Contains("Reason: Budget exceeded for current quarter", body);
        }

        [Fact]
        public async Task ResendEmailService_DispatchesExpectedHttpRequest()
        {
            string? capturedBody = null;
            string? capturedAuthScheme = null;
            string? capturedAuthParam = null;
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>(async (req, _) =>
                {
                    capturedAuthScheme = req.Headers.Authorization?.Scheme;
                    capturedAuthParam = req.Headers.Authorization?.Parameter;
                    if (req.Content != null)
                    {
                        capturedBody = await req.Content.ReadAsStringAsync();
                    }
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("{\"id\":\"email_123\"}")
                    };
                });

            var httpClient = new HttpClient(handlerMock.Object);
            var options = Options.Create(new ResendOptions
            {
                ApiKey = "re_test_api_key_123",
                FromEmail = "notifications@procura.com",
                FromName = "Procura"
            });
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<ResendEmailService>>();

            var service = new ResendEmailService(httpClient, options, configMock.Object, loggerMock.Object);

            await service.SendProcurementDecisionEmailAsync(
                "requester@example.com",
                "Jane Doe",
                "PR-1024",
                "MacBook Pro",
                "APPROVED",
                null,
                "John Manager");

            Assert.Equal("Bearer", capturedAuthScheme);
            Assert.Equal("re_test_api_key_123", capturedAuthParam);
            Assert.NotNull(capturedBody);

            using var doc = JsonDocument.Parse(capturedBody);
            var root = doc.RootElement;

            Assert.Equal("Procura <notifications@procura.com>", root.GetProperty("from").GetString());
            Assert.Equal("requester@example.com", root.GetProperty("to")[0].GetString());
            Assert.Equal("Procurement Request Approved — PR-1024", root.GetProperty("subject").GetString());
            Assert.Contains("PR-1024", root.GetProperty("text").GetString());
        }

        [Fact]
        public async Task ResendEmailService_WhenApiKeyMissing_LogsWarningAndDoesNotCallHttp()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(handlerMock.Object);
            var options = Options.Create(new ResendOptions { ApiKey = "" });
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<ResendEmailService>>();

            var service = new ResendEmailService(httpClient, options, configMock.Object, loggerMock.Object);

            await service.SendProcurementDecisionEmailAsync(
                "requester@example.com",
                "Jane Doe",
                "PR-1024",
                "MacBook Pro",
                "APPROVED",
                null,
                "John Manager");

            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }
    }
}
