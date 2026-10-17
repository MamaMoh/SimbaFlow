using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SimbaFlow.Application.Common.Email;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Infrastructure.Options;

namespace SimbaFlow.API.Features.Tenants;

/// <summary>
/// The welcome email a new agency owner gets: a link to choose their own password.
///
/// Before this the platform admin typed a temporary password and handed it over in a chat. The
/// link is a password-reset token, so it works whatever the account's current password is, and
/// choosing a password through it also clears the "must change password" flag.
/// </summary>
public sealed class AccountInvite
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _email;
    private readonly IOptionsMonitor<EmailOptions> _options;

    public AccountInvite(
        UserManager<ApplicationUser> userManager, IEmailService email, IOptionsMonitor<EmailOptions> options)
    {
        _userManager = userManager;
        _email = email;
        _options = options;
    }

    /// <summary>False when nothing could be sent: mail is not set up, or the host refused it.</summary>
    public async Task<bool> SendAsync(ApplicationUser user, string agencyName, CancellationToken ct)
    {
        if (!_email.IsConfigured || string.IsNullOrWhiteSpace(user.Email))
            return false;

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var baseUrl = _options.CurrentValue.AppBaseUrl.TrimEnd('/');
        var link = $"{baseUrl}/reset-password?email={HttpUtility.UrlEncode(user.Email)}&token={encoded}";

        var body = EmailLayout.Render(new EmailContent(
            Kicker: "Welcome",
            Title: "Your agency is on SimbaFlow",
            Greeting: EmailLayout.Greeting(user.FirstName),
            BodyHtml:
                EmailLayout.Paragraph(
                    $"Your agency, <b>{EmailLayout.Encode(agencyName)}</b>, now has a SimbaFlow account, "
                    + "and you are its owner. Choose a password to sign in for the first time.")
                + EmailLayout.SmallPrint($"Your username is <b>{EmailLayout.Encode(user.UserName)}</b>."),
            Button: new EmailButton("Choose your password", link),
            NoteHtml: "<b style=\"color:#2b3631;\">Link run out?</b> It works for a day. Use \"Forgot "
                + "password\" on the sign-in page, or reply to this email and we'll send a new one."));

        return await _email.SendAsync(user.Email, $"Welcome to SimbaFlow, {agencyName}", body, ct);
    }

    /// <summary>
    /// A password nobody is told, for an owner who will choose their own through the invite.
    /// Drawn from every class the password policy asks for.
    /// </summary>
    public static string UnusablePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz",
            digits = "23456789", symbols = "!@#$%^&*-_=+";
        var all = upper + lower + digits + symbols;
        var chars = new List<char> { Pick(upper), Pick(lower), Pick(digits), Pick(symbols) };
        while (chars.Count < 32) chars.Add(Pick(all));
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());

        static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
    }
}
