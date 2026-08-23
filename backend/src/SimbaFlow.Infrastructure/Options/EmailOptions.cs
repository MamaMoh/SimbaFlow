namespace SimbaFlow.Infrastructure.Options;

/// <summary>
/// SMTP settings, bound from the "Email:ET" configuration section.
///
/// The section is keyed by country so a second market can be added without reshaping config.
/// Password is never committed — supply it through user-secrets locally and the environment
/// (Email__ET__Password) on the server.
/// </summary>
public sealed class EmailOptions
{
    public string MailServer { get; set; } = string.Empty;
    public int MailPort { get; set; } = 587;
    public string Sender { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SenderName { get; set; } = "SimbaFlow";

    /// <summary>Base URL used to build links in emails, e.g. https://app.laba.et.</summary>
    public string AppBaseUrl { get; set; } = string.Empty;

    /// <summary>Nothing is sent until a server and password are configured.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(MailServer)
        && !string.IsNullOrWhiteSpace(Sender)
        && !string.IsNullOrWhiteSpace(Password);
}
