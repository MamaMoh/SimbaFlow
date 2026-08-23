namespace SimbaFlow.Application.Common.Interfaces;

public interface IEmailService
{
    /// <summary>False when SMTP is not configured — callers should degrade, not fail.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Sends one message. Returns false on failure rather than throwing: a user-facing action
    /// (registering an agency, requesting a reset) must not 500 because the mail host is down.
    /// </summary>
    Task<bool> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
