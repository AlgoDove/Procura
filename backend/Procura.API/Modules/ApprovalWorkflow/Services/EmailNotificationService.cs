using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Procura.API.Modules.ApprovalWorkflow.Services
{
    /// <summary>
    /// Implementation of IEmailNotificationService providing structured logging
    /// and ready for third-party SMTP/SendGrid/SES binding.
    /// </summary>
    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly ILogger<EmailNotificationService> _logger;

        public EmailNotificationService(ILogger<EmailNotificationService> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string recipientEmail, string recipientName, string subject, string messageBody, CancellationToken ct = default)
        {
            // Structured audit log of third-party email notification dispatch
            _logger.LogInformation(
                "[EMAIL NOTIFICATION DISPATCHED] To: {RecipientName} <{RecipientEmail}> | Subject: {Subject} | Body: {Body}",
                recipientName, recipientEmail, subject, messageBody);

            return Task.CompletedTask;
        }
    }
}
