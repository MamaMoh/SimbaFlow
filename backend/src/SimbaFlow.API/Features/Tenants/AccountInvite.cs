using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
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

        var name = string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName;
        var body = $"""
            <p>Hello {HttpUtility.HtmlEncode(name)},</p>
            <p>Your agency, <b>{HttpUtility.HtmlEncode(agencyName)}</b>, now has a SimbaFlow account, and you
            are its owner. Choose a password to sign in for the first time.</p>
            <p><a href="{link}" style="background:#1B5E3F;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none;display:inline-block">Choose your password</a></p>
            <p style="color:#555;font-size:13px">Your username is <b>{HttpUtility.HtmlEncode(user.UserName)}</b>.
            The link works for a day; if it has run out, use "Forgot password" on the sign-in page or ask us to send a new one.</p>
            <p style="color:#555;font-size:13px">If the button does not work, paste this into your browser:<br>{link}</p>
            """;

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
