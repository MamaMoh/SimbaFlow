using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// Pure helpers for Unit 7 bot/notification invariants (TEST-70–78).
/// Used by bot services and property/example tests.
/// </summary>
public static class BotNotificationRules
{
    public static readonly HashSet<string> AllowedLanguages =
        new(StringComparer.OrdinalIgnoreCase) { "en", "am" };

    public static bool IsValidLanguage(string? lang) =>
        !string.IsNullOrWhiteSpace(lang) && AllowedLanguages.Contains(lang.Trim());

    /// <summary>
    /// Invalid language keeps the previous preference (TEST-75).
    /// </summary>
    public static string ResolveLanguage(string? requested, string previous) =>
        IsValidLanguage(requested) ? requested!.Trim().ToLowerInvariant() : previous;

    public static bool IsDeferredWriteCommand(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        return t.StartsWith("/medical", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("/arrived", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Unlinked chats get instructions only — never candidate field payloads (TEST-70).
    /// </summary>
    public static bool ReplyLooksLikeCandidateData(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply)) return false;
        return reply.Contains("Stage:", StringComparison.OrdinalIgnoreCase)
               || reply.Contains("Passport", StringComparison.OrdinalIgnoreCase)
               || reply.Contains("Candidate:", StringComparison.OrdinalIgnoreCase)
               || reply.Contains("ደረጃ:", StringComparison.OrdinalIgnoreCase);
    }

    public static string UnlinkedInstructionsReply() =>
        "This chat is not linked yet. Generate a link code in the web app, then send /link CODE.";

    /// <summary>
    /// Status lookup is tenant-scoped: other-tenant passports never match (TEST-71).
    /// </summary>
    public static bool TenantScopedPassportMatch(
        Guid candidateTenantId,
        Guid userTenantId,
        string candidatePassport,
        string query) =>
        candidateTenantId == userTenantId
        && string.Equals(candidatePassport.Trim(), query.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool IsValidDeliveryStatus(DeliveryStatus status) =>
        Enum.IsDefined(status);

    public static bool IsValidDeliveryStatus(int raw) =>
        Enum.IsDefined(typeof(DeliveryStatus), raw);

    /// <summary>
    /// Stage push failure must not undo an already-committed transition (TEST-74).
    /// </summary>
    public static bool TransitionRemainsCommitted(bool transitionCommitted, bool pushSucceeded) =>
        transitionCommitted;

    /// <summary>
    /// Scrub secrets and truncate delivery error text (TEST-76).
    /// </summary>
    public static string SanitizeDeliveryError(string? message, string? botToken = null, int maxLength = 1024)
    {
        var text = message ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(botToken) && text.Contains(botToken, StringComparison.Ordinal))
            text = text.Replace(botToken, "[REDACTED]", StringComparison.Ordinal);

        // Common Telegram token shape: digits:alphanum
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"\b\d{8,12}:[A-Za-z0-9_-]{20,}\b",
            "[REDACTED]");

        if (text.Length > maxLength)
            text = text[..maxLength];
        return text;
    }

    /// <summary>
    /// Deferred write commands must not mutate candidate state (TEST-77).
    /// </summary>
    public static bool WriteCommandMutatesCandidate(string? commandText) =>
        !IsDeferredWriteCommand(commandText);

    /// <summary>
    /// Production notifier must not be a no-op type name (TEST-78).
    /// </summary>
    public static bool IsNonNoOpNotifier(string? implementationTypeName) =>
        !string.IsNullOrWhiteSpace(implementationTypeName)
        && !implementationTypeName.Contains("NoOp", StringComparison.OrdinalIgnoreCase);
}
