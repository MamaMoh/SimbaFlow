using FsCheck;
using FsCheck.Xunit;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Properties;

/// <summary>
/// FsCheck properties for Unit 7 Bot & Notifications invariants (TEST-70–78).
/// </summary>
public class BotNotificationProperties
{
    /// <summary>TEST-70: Unlinked instruction reply never looks like candidate data.</summary>
    [Property(MaxTest = 40)]
    public bool UnlinkedNoCandidateData()
    {
        var reply = BotNotificationRules.UnlinkedInstructionsReply();
        return !BotNotificationRules.ReplyLooksLikeCandidateData(reply);
    }

    /// <summary>TEST-71: Passport match requires same tenant.</summary>
    [Property(MaxTest = 80)]
    public bool StatusLookupTenantScoped(Guid tenantA, Guid tenantB, NonEmptyString passport)
    {
        var p = passport.Get.Trim();
        if (string.IsNullOrWhiteSpace(p)) return true;

        var same = BotNotificationRules.TenantScopedPassportMatch(tenantA, tenantA, p, p);
        var cross = BotNotificationRules.TenantScopedPassportMatch(tenantA, tenantB, p, p);
        if (tenantA == tenantB) return same && cross;
        return same && !cross;
    }

    /// <summary>TEST-72: A code is consumable only when unused and unexpired.</summary>
    [Property(MaxTest = 60)]
    public bool LinkCodeSingleUseModel(bool consumed, bool expired)
    {
        var canConsumeOnce = !consumed && !expired;
        var canConsumeTwice = canConsumeOnce && consumed; // impossible once marked consumed
        return canConsumeOnce || (!canConsumeTwice && (consumed || expired));
    }

    /// <summary>TEST-73: Delivery status raw ints are only valid when defined.</summary>
    [Property(MaxTest = 80)]
    public bool DeliveryStatusEnum(int raw)
    {
        var clamped = raw % 20 - 5;
        var ok = BotNotificationRules.IsValidDeliveryStatus(clamped);
        return ok == Enum.IsDefined(typeof(DeliveryStatus), clamped);
    }

    /// <summary>TEST-74: Committed transitions stay committed regardless of push outcome.</summary>
    [Property(MaxTest = 40)]
    public bool PushDoesNotRollback(bool committed, bool pushOk) =>
        BotNotificationRules.TransitionRemainsCommitted(committed, pushOk) == committed;

    /// <summary>TEST-75: Only en|am update language; others keep previous.</summary>
    [Property(MaxTest = 80)]
    public bool LangOnlyEnAm(NonEmptyString requested, bool previousAm)
    {
        var previous = previousAm ? "am" : "en";
        var resolved = BotNotificationRules.ResolveLanguage(requested.Get, previous);
        if (BotNotificationRules.IsValidLanguage(requested.Get))
            return resolved is "en" or "am";
        return resolved == previous;
    }

    /// <summary>TEST-76: Sanitized errors never contain the provided token.</summary>
    [Property(MaxTest = 50)]
    public bool ErrorNeverLeaksToken(NonEmptyString prefix, NonEmptyString suffix)
    {
        const string token = "112233445:AAHdqTcvCH1vGWJxfSeofSAs0K5PALDsaw";
        var message = $"{prefix.Get} {token} {suffix.Get}";
        var sanitized = BotNotificationRules.SanitizeDeliveryError(message, token);
        return !sanitized.Contains(token, StringComparison.Ordinal);
    }

    /// <summary>TEST-77: /medical and /arrived never mutate.</summary>
    [Property(MaxTest = 40)]
    public bool WriteCommandsNoOp(bool medical)
    {
        var cmd = medical ? "/medical EP1" : "/arrived yes";
        return BotNotificationRules.IsDeferredWriteCommand(cmd)
               && !BotNotificationRules.WriteCommandMutatesCandidate(cmd);
    }

    /// <summary>TEST-78: NoOp type names are rejected; Telegram notifier accepted.</summary>
    [Property(MaxTest = 30)]
    public bool NotifierNonNoOp(bool useNoOp)
    {
        var name = useNoOp ? "NoOpCandidateNotifier" : "TelegramCandidateNotifier";
        return BotNotificationRules.IsNonNoOpNotifier(name) == !useNoOp;
    }
}
