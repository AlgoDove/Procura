using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Procura.API.Modules.ApprovalWorkflow.Services
{
    /// <summary>
    /// Implementation of IEmailNotificationService dispatching emails via Resend REST API.
    /// </summary>
    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailNotificationService> _logger;

        public EmailNotificationService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<EmailNotificationService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendEmailAsync(string recipientEmail, string recipientName, string subject, string messageBody, CancellationToken ct = default)
        {
            var apiKey = _configuration["Resend:ApiKey"] ?? _configuration["RESEND_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "re_YOUR_API_KEY_HERE")
            {
                _logger.LogWarning("Resend API key is not configured. Email to {Email} logged but not sent via Resend: [Subject: {Subject}] {Body}",
                    recipientEmail, subject, messageBody);
                return;
            }

            var senderEmail = _configuration["Resend:SenderEmail"] ?? "onboarding@resend.dev";
            var senderName = _configuration["Resend:SenderName"] ?? "Procura Notifications";
            var from = string.IsNullOrWhiteSpace(senderName)
                ? senderEmail
                : $"{senderName} <{senderEmail}>";

            var encodedSubject = System.Net.WebUtility.HtmlEncode(subject);
            var encodedBody = System.Net.WebUtility.HtmlEncode(messageBody).Replace("\n", "<br/>");
            var html = $"<div style=\"font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;\"><h2 style=\"color: #1e3a5f; margin-bottom: 16px;\">{encodedSubject}</h2><p style=\"font-size: 15px;\">{encodedBody}</p><hr style=\"border: none; border-top: 1px solid #eaeaea; margin: 24px 0;\" /><p style=\"font-size: 12px; color: #888;\">This is an automated notification from Procura Procurement System.</p></div>";

            var payload = new
            {
                from,
                to = new[] { recipientEmail },
                subject,
                html,
                text = messageBody
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            try
            {
                var response = await _httpClient.SendAsync(request, ct);
                var responseBody = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[RESEND SUCCESS] Dispatched email to {RecipientEmail} | Subject: {Subject} | Response: {Response}",
                        recipientEmail, subject, responseBody);
                }
                else
                {
                    _logger.LogWarning("[RESEND WARNING] Failed to send email to {RecipientEmail}. Status: {StatusCode} | Response: {Response}",
                        recipientEmail, response.StatusCode, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RESEND ERROR] Exception calling Resend API for recipient {RecipientEmail}", recipientEmail);
                throw;
            }
        }
    }
}
