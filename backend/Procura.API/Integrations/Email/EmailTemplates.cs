using System;
using System.Net;
using System.Text;

namespace Procura.API.Integrations.Email
{
    /// <summary>
    /// Generates structured subjects, HTML bodies, and plain text bodies for procurement decision emails.
    /// </summary>
    public static class EmailTemplates
    {
        public static string BuildSubject(string decision, string requestNumber)
        {
            var isApproved = string.Equals(decision, "APPROVED", StringComparison.OrdinalIgnoreCase);
            return isApproved
                ? $"Procurement Request Approved — {requestNumber}"
                : $"Procurement Request Rejected — {requestNumber}";
        }

        public static string BuildPlainTextBody(
            string requesterName,
            string requestNumber,
            string requestTitle,
            string decision,
            string? rejectionReason,
            string? approvedBy)
        {
            var isApproved = string.Equals(decision, "APPROVED", StringComparison.OrdinalIgnoreCase);
            var sb = new StringBuilder();

            sb.AppendLine($"Hello {requesterName},");
            sb.AppendLine();

            if (isApproved)
            {
                sb.AppendLine("Your procurement request has been approved.");
                sb.AppendLine();
                sb.AppendLine($"Request Number: {requestNumber}");
                sb.AppendLine($"Request: {requestTitle}");
                sb.AppendLine("Status: APPROVED");
                sb.AppendLine($"Approved By: {approvedBy ?? "Manager"}");
                sb.AppendLine();
                sb.AppendLine("Your procurement request has successfully passed the approval process.");
            }
            else
            {
                sb.AppendLine("Your procurement request has been rejected.");
                sb.AppendLine();
                sb.AppendLine($"Request Number: {requestNumber}");
                sb.AppendLine($"Request: {requestTitle}");
                sb.AppendLine("Status: REJECTED");
                sb.AppendLine($"Reviewed By: {approvedBy ?? "Manager"}");
                sb.AppendLine($"Reason: {rejectionReason}");
                sb.AppendLine();
                sb.AppendLine("Please review the reason above and contact the relevant manager if further clarification is required.");
            }

            sb.AppendLine();
            sb.AppendLine("Regards,");
            sb.AppendLine("Procura Procurement Management System");

            return sb.ToString();
        }

        public static string BuildHtmlBody(
            string requesterName,
            string requestNumber,
            string requestTitle,
            string decision,
            string? rejectionReason,
            string? approvedBy)
        {
            var isApproved = string.Equals(decision, "APPROVED", StringComparison.OrdinalIgnoreCase);
            var statusColor = isApproved ? "#15803d" : "#b91c1c";
            var statusText = isApproved ? "APPROVED" : "REJECTED";
            var reviewerLabel = isApproved ? "Approved By" : "Reviewed By";

            var safeName = WebUtility.HtmlEncode(requesterName);
            var safeNumber = WebUtility.HtmlEncode(requestNumber);
            var safeTitle = WebUtility.HtmlEncode(requestTitle);
            var safeReviewer = WebUtility.HtmlEncode(approvedBy ?? "Manager");
            var safeReason = !string.IsNullOrWhiteSpace(rejectionReason) ? WebUtility.HtmlEncode(rejectionReason) : null;

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset=\"utf-8\"><style>");
            sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #1e293b; background-color: #f8fafc; margin: 0; padding: 24px; }");
            sb.AppendLine(".card { max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 8px; border: 1px solid #e2e8f0; padding: 32px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }");
            sb.AppendLine(".badge { display: inline-block; padding: 4px 10px; font-weight: 700; font-size: 13px; border-radius: 4px; color: #ffffff; background-color: " + statusColor + "; }");
            sb.AppendLine(".meta { margin: 20px 0; background: #f8fafc; border-left: 4px solid " + statusColor + "; padding: 12px 16px; border-radius: 4px; }");
            sb.AppendLine(".meta-row { margin: 6px 0; font-size: 14px; }");
            sb.AppendLine(".meta-label { font-weight: 600; color: #475569; width: 130px; display: inline-block; }");
            sb.AppendLine(".reason-box { margin-top: 12px; padding: 12px; background: #fef2f2; border: 1px solid #fecaca; border-radius: 4px; color: #991b1b; }");
            sb.AppendLine(".footer { margin-top: 28px; padding-top: 16px; border-top: 1px solid #e2e8f0; font-size: 13px; color: #64748b; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<div class=\"card\">");
            sb.AppendLine($"<p>Hello <strong>{safeName}</strong>,</p>");

            if (isApproved)
            {
                sb.AppendLine("<p>Your procurement request has been <strong>approved</strong>.</p>");
            }
            else
            {
                sb.AppendLine("<p>Your procurement request has been <strong>rejected</strong>.</p>");
            }

            sb.AppendLine("<div class=\"meta\">");
            sb.AppendLine($"<div class=\"meta-row\"><span class=\"meta-label\">Request Number:</span> <strong>{safeNumber}</strong></div>");
            sb.AppendLine($"<div class=\"meta-row\"><span class=\"meta-label\">Request:</span> {safeTitle}</div>");
            sb.AppendLine($"<div class=\"meta-row\"><span class=\"meta-label\">Status:</span> <span class=\"badge\">{statusText}</span></div>");
            sb.AppendLine($"<div class=\"meta-row\"><span class=\"meta-label\">{reviewerLabel}:</span> {safeReviewer}</div>");

            if (!isApproved && safeReason != null)
            {
                sb.AppendLine($"<div class=\"reason-box\"><strong>Reason:</strong> {safeReason}</div>");
            }
            sb.AppendLine("</div>");

            if (isApproved)
            {
                sb.AppendLine("<p>Your procurement request has successfully passed the approval process.</p>");
            }
            else
            {
                sb.AppendLine("<p>Please review the reason above and contact the relevant manager if further clarification is required.</p>");
            }

            sb.AppendLine("<div class=\"footer\">");
            sb.AppendLine("<p>Regards,<br/><strong>Procura Procurement Management System</strong></p>");
            sb.AppendLine("</div></div></body></html>");

            return sb.ToString();
        }
    }
}
