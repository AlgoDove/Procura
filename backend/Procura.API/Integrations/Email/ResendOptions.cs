namespace Procura.API.Integrations.Email
{
    /// <summary>
    /// Configuration options for the Resend transactional email integration.
    /// </summary>
    public class ResendOptions
    {
        public const string SectionName = "Resend";

        /// <summary>
        /// API key for authenticating with the Resend REST API (re_...).
        /// Configured via User Secrets or environment variables.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Sender email address (e.g. onboarding@resend.dev or verified domain).
        /// </summary>
        public string FromEmail { get; set; } = "onboarding@resend.dev";

        /// <summary>
        /// Display name for the sender.
        /// </summary>
        public string FromName { get; set; } = "Procura";
    }
}
