using System.Threading.Tasks;

namespace Procura.API.Integrations.Email
{
    /// <summary>
    /// Service contract representing transactional email dispatching.
    /// Decouples the core Approval Workflow from specific third-party email providers.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Sends a transactional email notification when a final manager decision (APPROVED or REJECTED) is made.
        /// </summary>
        /// <param name="recipientEmail">The requester's email address.</param>
        /// <param name="requesterName">The requester's full name.</param>
        /// <param name="procurementRequestNumber">The human-readable request number (e.g. PR-1024).</param>
        /// <param name="requestTitle">The request title/summary.</param>
        /// <param name="decision">The decision outcome: APPROVED or REJECTED.</param>
        /// <param name="rejectionReason">The rejection reason/justification (only populated for REJECTED decisions).</param>
        /// <param name="approvedBy">The reviewing manager's name.</param>
        Task SendProcurementDecisionEmailAsync(
            string recipientEmail,
            string requesterName,
            string procurementRequestNumber,
            string requestTitle,
            string decision,
            string? rejectionReason,
            string? approvedBy);
    }
}
