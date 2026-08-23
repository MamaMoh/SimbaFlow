using System.Security.Cryptography;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// Link codes that tie a Telegram chat to a SimbaFlow account.
///
/// These were six digits with no attempt limit, matched across every tenant. A million
/// possibilities is nothing to a script: sending the bot random numbers until one landed attached
/// the sender's Telegram to a stranger's account — including an account in another agency — with
/// that person's data and permissions.
///
/// The code is now drawn from a 32-character alphabet over 8 positions (~1.1 x 10^12), which is
/// far beyond reach inside a ten-minute window, and a per-chat attempt limit stops the probing
/// long before that anyway.
/// </summary>
public static class BotLinkCodeRules
{
    /// <summary>
    /// Crockford-style alphabet: no 0/O, 1/I/L, U. Codes get read off a screen and typed on a
    /// phone, so the characters people confuse are simply not in the set.
    /// </summary>
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    public const int CodeLength = 8;

    /// <summary>Failed attempts allowed from one chat before it must wait.</summary>
    public const int MaxAttemptsPerChat = 5;

    /// <summary>How long a chat is refused after using up its attempts.</summary>
    public static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);

    /// <summary>Cryptographically random — Random.Shared is predictable from prior outputs.</summary>
    public static string Generate()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    /// <summary>
    /// Codes are stored and compared upper-case; phone keyboards autocapitalise inconsistently and
    /// a case mismatch would look to the user like a wrong code.
    /// </summary>
    public static string Normalize(string? raw) =>
        (raw ?? string.Empty).Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();

    /// <summary>
    /// True when a message is plausibly a link code, so a bare paste can be treated as one.
    /// Deliberately strict: a passport number must not be swallowed as a link attempt.
    /// </summary>
    public static bool LooksLikeLinkCode(string? raw)
    {
        var text = Normalize(raw);
        if (text.Length != CodeLength) return false;
        foreach (var c in text)
            if (!Alphabet.Contains(c)) return false;
        return true;
    }
}
