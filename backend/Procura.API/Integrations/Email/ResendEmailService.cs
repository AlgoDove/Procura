using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Procura.API.Integrations.Email
{
    /// <summary>
    /// Implementation of IEmailService using the Resend REST API (https://api.resend.com/emails).
    /// </summary>
    public class ResendEmailService : IEmailService
    {
        private const string ResendEndpoint = "https://api.resend.com/emails";

        private readonly HttpClient _httpClient;
        private readonly ResendOptions _options;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ResendEmailService> _logger;

        public ResendEmailService(
            HttpClient httpClient,
            IOptions<ResendOptions> options,
            IConfiguration configuration,
            ILogger<ResendEmailService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options?.Value ?? new ResendOptions();
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendProcurementDecisionEmailAsync(
            string recipientEmail,
            string requesterName,
            string procurementRequestNumber,
            string requestTitle,
            string decision,
            string? rejectionReason,
            string? approvedBy)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                _logger.LogWarning("Recipient email is empty for request {RequestNumber}. Skipping email dispatch.", procurementRequestNumber);
                return;
            }

            var apiKey = !string.IsNullOrWhiteSpace(_options.ApiKey) && _options.ApiKey != "re_YOUR_API_KEY_HERE"
                ? _options.ApiKey
                : _configuration["Resend:ApiKey"] ?? _configuration["RESEND_API_KEY"];

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "re_YOUR_API_KEY_HERE")
            {
                _logger.LogWarning("Resend API key is not configured. Email to {Email} for request {RequestNumber} skipped.",
                    recipientEmail, procurementRequestNumber);
                return;
            }

            var fromEmail = !string.IsNullOrWhiteSpace(_options.FromEmail)
                ? _options.FromEmail
                : _configuration["Resend:FromEmail"] ?? _configuration["Resend:SenderEmail"] ?? "onboarding@resend.dev";

            var fromName = !string.IsNullOrWhiteSpace(_options.FromName)
                ? _options.FromName
                : _configuration["Resend:FromName"] ?? _configuration["Resend:SenderName"] ?? "Procura";

            var fromAddress = string.IsNullOrWhiteSpace(fromName)
                ? fromEmail
                : $"{fromName} <{fromEmail}>";

            var subject = EmailTemplates.BuildSubject(decision, procurementRequestNumber);
            var htmlBody = EmailTemplates.BuildHtmlBody(requesterName, procurementRequestNumber, requestTitle, decision, rejectionReason, approvedBy);
            var textBody = EmailTemplates.BuildPlainTextBody(requesterName, procurementRequestNumber, requestTitle, decision, rejectionReason, approvedBy);

            var payload = new
            {
                from = fromAddress,
                to = new[] { recipientEmail.Trim() },
                subject = subject,
                html = htmlBody,
                text = textBody
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, ResendEndpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            try
            {
                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Procurement decision email successfully sent via Resend to {RecipientEmail} for request {RequestNumber}. Response: {Response}",
                        recipientEmail, procurementRequestNumber, responseBody);
                }
                else
                {
                    _logger.LogWarning(
                        "Resend API returned non-success status code {StatusCode} for request {RequestNumber}. Response: {Response}",
                        response.StatusCode, procurementRequestNumber, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to communicate with Resend API when sending decision email for request {RequestNumber} to {RecipientEmail}",
                    procurementRequestNumber, recipientEmail);
                throw;
            }
        }
    }
}
