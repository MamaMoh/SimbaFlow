using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Services;

/// <summary>Lifecycle state of a partner agreement (ትስስር), derived from its dates + status.</summary>
public enum AgreementState
{
    /// <summary>Agreement start date is in the future.</summary>
    NotStarted,

    /// <summary>In force with comfortable time remaining.</summary>
    Active,

    /// <summary>In force but ending within <see cref="PartnerAgreementRules.ExpiringSoonDays"/>.</summary>
    ExpiringSoon,

    /// <summary>End date has passed (or the link was marked Expired).</summary>
    Expired,

    /// <summary>Administratively suspended regardless of dates.</summary>
    Suspended
}

/// <summary>
/// Partner agreement expiry rules. Agreements are typically renewed every ~2 years; an agency may
/// not place candidates through a partner whose agreement has lapsed (MoLS Directive 1126/2018),
/// so this is the one place that decides both what staff see and what the server accepts.
/// </summary>
public static class PartnerAgreementRules
{
    /// <summary>Warn this many days before the agreement ends.</summary>
    public const int ExpiringSoonDays = 60;

    public static AgreementState Evaluate(
        DateOnly agreementStart,
        DateOnly agreementEnd,
        PartnerLinkStatus status,
        DateOnly today)
    {
        if (status == PartnerLinkStatus.Suspended) return AgreementState.Suspended;

        // An explicit Expired status, or a lapsed end date, both mean expired.
        if (status == PartnerLinkStatus.Expired || agreementEnd < today)
            return AgreementState.Expired;

        if (agreementStart > today) return AgreementState.NotStarted;

        return DaysRemaining(agreementEnd, today) <= ExpiringSoonDays
            ? AgreementState.ExpiringSoon
            : AgreementState.Active;
    }

    /// <summary>Days until the agreement ends; negative once it has lapsed.</summary>
    public static int DaysRemaining(DateOnly agreementEnd, DateOnly today) =>
        agreementEnd.DayNumber - today.DayNumber;

    /// <summary>
    /// True when a candidate may be placed through this agreement. Expiring-soon still counts —
    /// the agency is warned but not blocked until the end date actually passes.
    /// </summary>
    public static bool IsUsableForIntake(AgreementState state) =>
        state is AgreementState.Active or AgreementState.ExpiringSoon;

    public static bool IsUsableForIntake(
        DateOnly agreementStart,
        DateOnly agreementEnd,
        PartnerLinkStatus status,
        DateOnly today) =>
        IsUsableForIntake(Evaluate(agreementStart, agreementEnd, status, today));

    /// <summary>Short human label for chips/alerts.</summary>
    public static string Describe(AgreementState state, int daysRemaining) => state switch
    {
        AgreementState.Expired => daysRemaining < 0
            ? $"Expired {Math.Abs(daysRemaining)} day(s) ago"
            : "Expired",
        AgreementState.ExpiringSoon => $"Ends in {daysRemaining} day(s)",
        AgreementState.NotStarted => "Not started yet",
        AgreementState.Suspended => "Suspended",
        _ => "Active"
    };
}
