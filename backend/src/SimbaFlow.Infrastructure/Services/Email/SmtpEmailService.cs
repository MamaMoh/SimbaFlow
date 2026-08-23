using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Infrastructure.Options;

namespace SimbaFlow.Infrastructure.Services.Email;

/// <summary>
/// SMTP sender (Zoho in production).
///
/// Port 587 means STARTTLS, not implicit SSL — connecting with SslOnConnect there hangs until it
/// times out, which is the usual way this is misconfigured. Port 465 is the implicit-TLS port.
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptionsMonitor<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured => _options.CurrentValue.IsConfigured;

    public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;

        if (!o.IsConfigured)
        {
            _logger.LogWarning("Email not sent to {To}: SMTP is not configured", Redact(toEmail));
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(o.SenderName, o.Sender));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            var security = o.MailPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(o.MailServer, o.MailPort, security, ct);
            await client.AuthenticateAsync(o.Sender, o.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email sent to {To}: {Subject}", Redact(toEmail), subject);
            return true;
        }
        catch (Exception ex)
        {
            // Never log the message body or the password; the address is redacted too.
            _logger.LogError(ex, "Email to {To} failed", Redact(toEmail));
            return false;
        }
    }

    /// <summary>Keeps enough of the address to debug with, without writing it whole into logs.</summary>
    private static string Redact(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return "***";
        return $"{email[0]}***{email[at..]}";
    }
}
