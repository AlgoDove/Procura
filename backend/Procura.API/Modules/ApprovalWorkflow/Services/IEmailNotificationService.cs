using System.Threading;
using System.Threading.Tasks;

namespace Procura.API.Modules.ApprovalWorkflow.Services
{
    /// <summary>
    /// Service contract representing the third-party email notification integration.
    /// Dispatches email alerts for critical workflow milestones.
    /// </summary>
    public interface IEmailNotificationService
    {
        /// <summary>
        /// Sends an email notification to the specified recipient.
        /// </summary>
        Task SendEmailAsync(string recipientEmail, string recipientName, string subject, string messageBody, CancellationToken ct = default);
    }
}
