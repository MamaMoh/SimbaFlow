using System.Net;
using System.Text;

namespace SimbaFlow.Application.Common.Email;

/// <summary>A call to action: the one thing the email asks the reader to do.</summary>
/// <param name="Text">What the button says: "Choose a new password".</param>
/// <param name="Url">Where it goes. Shown again under the button for clients that drop buttons.</param>
public sealed record EmailButton(string Text, string Url);

/// <summary>
/// What one email says. <see cref="EmailLayout.Render"/> draws it.
/// </summary>
/// <param name="Kicker">The small capitals over the title, naming the kind of email: "Account security".</param>
/// <param name="Title">The heading in the green banner: "Reset your password".</param>
/// <param name="BodyHtml">
/// The message itself, as HTML paragraphs — build them with <see cref="EmailLayout.Paragraph"/>.
/// Trusted markup: anything that came from a person must be encoded before it gets here.
/// </param>
/// <param name="Greeting">The person's first name, or null for "Hello,". See <see cref="EmailLayout.Greeting"/>.</param>
/// <param name="Button">The call to action, if the email has one.</param>
/// <param name="NoteHtml">A short aside in a shaded box under the button, e.g. "Didn't ask for this?".</param>
public sealed record EmailContent(
    string Kicker,
    string Title,
    string BodyHtml,
    string? Greeting = null,
    EmailButton? Button = null,
    string? NoteHtml = null);

/// <summary>
/// The one layout every email SimbaFlow sends is drawn in: a green banner carrying the title, the
/// message on white, one button, an optional shaded note, and the same footer on all of them.
///
/// Each email used to be a few bare paragraphs with its own inline button, so a password reset, an
/// agency's welcome and a billing reminder each looked like they came from somewhere different, and
/// none looked like they came from a company. One layout means one place to change how SimbaFlow
/// writes to people.
///
/// Built from tables and inline styles, because that is what email clients render: Gmail and Outlook
/// drop &lt;style&gt; blocks and most of CSS layout. Fixed at 600 pixels wide and shrinking below
/// that, so it reads on a phone. No images, so nothing is hidden behind "show images" and nothing
/// depends on the app being reachable when the email is opened.
/// </summary>
public static class EmailLayout
{
    private const string Green = "#006633";
    private const string Ink = "#2b3631";
    private const string Muted = "#8a958f";
    private const string Font = "Segoe UI,Helvetica,Arial,sans-serif";

    /// <summary>One paragraph of body text, already-safe HTML inside. Encode user text first.</summary>
    public static string Paragraph(string html) =>
        $"<p style=\"margin:0 0 14px;font-size:15px;line-height:1.6;color:{Ink};\">{html}</p>";

    /// <summary>A smaller, quieter paragraph for the detail under the main message.</summary>
    public static string SmallPrint(string html) =>
        $"<p style=\"margin:0 0 14px;font-size:13px;line-height:1.6;color:#5c6a62;\">{html}</p>";

    /// <summary>Text from a person or a record, made safe to put inside the HTML.</summary>
    public static string Encode(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    /// <summary>
    /// The first name to greet someone by, or null to say "Hello," on its own.
    ///
    /// Null for a placeholder rather than a person. The account the platform starts with is called
    /// "System Administrator", and its password reset arrived as "Hello System," — which reads as a
    /// machine talking to itself.
    /// </summary>
    public static string? Greeting(string? firstName)
    {
        var name = firstName?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        return Placeholders.Contains(name) ? null : name;
    }

    private static readonly HashSet<string> Placeholders =
        new(StringComparer.OrdinalIgnoreCase) { "System", "Admin", "Administrator", "User", "Test" };

    public static string Render(EmailContent content)
    {
        var e = Encode;
        var html = new StringBuilder();

        html.Append($"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{e(content.Title)}</title></head>
            <body style="margin:0;padding:0;background:#eef1ef;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#eef1ef;padding:32px 12px;font-family:{Font};">
            <tr><td align="center">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:12px;overflow:hidden;">
            <tr><td style="background:{Green};padding:28px 40px;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
                <td style="vertical-align:middle;font-size:18px;font-weight:700;color:#ffffff;font-family:{Font};">SimbaFlow</td>
                <td align="right" style="vertical-align:middle;font-size:12px;color:#bfe0cc;letter-spacing:.6px;text-transform:uppercase;font-family:{Font};">{e(content.Kicker)}</td>
              </tr></table>
              <h1 style="margin:22px 0 0;font-size:26px;line-height:1.25;color:#ffffff;font-weight:700;font-family:{Font};">{e(content.Title)}</h1>
            </td></tr>
            <tr><td style="padding:34px 40px 8px;">
            """);

        html.Append(Paragraph(content.Greeting is { } name ? $"Hello {e(name)}," : "Hello,"));
        html.Append(content.BodyHtml);

        if (content.Button is { } button)
        {
            html.Append($"""
                <table role="presentation" cellpadding="0" cellspacing="0" style="margin:12px 0 0;"><tr><td style="border-radius:8px;background:{Green};">
                  <a href="{e(button.Url)}" style="display:inline-block;padding:14px 28px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:8px;font-family:{Font};">{e(button.Text)}</a>
                </td></tr></table>
                """);
        }

        html.Append("</td></tr>");

        if (!string.IsNullOrWhiteSpace(content.NoteHtml))
        {
            html.Append($"""
                <tr><td style="padding:24px 40px 0;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f8f5;border-radius:8px;"><tr><td style="padding:14px 16px;font-size:13px;line-height:1.6;color:#4b5a52;font-family:{Font};">{content.NoteHtml}</td></tr></table>
                </td></tr>
                """);
        }

        // The address again, for clients that strip the button and for anyone forwarding the email
        // to a colleague whose client does.
        if (content.Button is { } fallback)
        {
            html.Append($"""
                <tr><td style="padding:22px 40px 0;font-size:12px;line-height:1.6;color:{Muted};font-family:{Font};">Button not working? Copy this link into your browser:<br><a href="{e(fallback.Url)}" style="color:{Green};word-break:break-all;">{e(fallback.Url)}</a></td></tr>
                """);
        }

        html.Append($"""
            <tr><td style="padding:32px 0 0;"></td></tr>
            <tr><td style="background:#f7f9f8;border-top:1px solid #e6ebe8;padding:18px 40px;font-size:12px;line-height:1.7;color:{Muted};font-family:{Font};">
              <b style="color:#5c6a62;">SimbaFlow</b> · Labour export agency management<br>
              Need help? Reply to this email and the SimbaFlow team will get back to you.
            </td></tr>
            </table>
            </td></tr></table>
            </body></html>
            """);

        return html.ToString();
    }
}
