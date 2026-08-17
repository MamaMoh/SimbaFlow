using FluentAssertions;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Partner agreement expiry decides both what staff can pick at intake and what the API accepts,
/// so the boundaries are pinned here.
/// </summary>
public class PartnerAgreementRulesTests
{
    private static readonly DateOnly Today = new(2026, 8, 17);

    private static AgreementState Eval(DateOnly start, DateOnly end,
        PartnerLinkStatus status = PartnerLinkStatus.Active) =>
        PartnerAgreementRules.Evaluate(start, end, status, Today);

    [Fact]
    public void WellInsideAgreement_IsActive()
    {
        Eval(Today.AddYears(-1), Today.AddYears(1)).Should().Be(AgreementState.Active);
    }

    [Fact]
    public void EndingWithin60Days_IsExpiringSoon()
    {
        Eval(Today.AddYears(-1), Today.AddDays(60)).Should().Be(AgreementState.ExpiringSoon);
        Eval(Today.AddYears(-1), Today.AddDays(1)).Should().Be(AgreementState.ExpiringSoon);
    }

    [Fact]
    public void EndingJustBeyondWindow_IsStillActive()
    {
        Eval(Today.AddYears(-1), Today.AddDays(61)).Should().Be(AgreementState.Active);
    }

    [Fact]
    public void EndsToday_IsExpiringSoon_NotExpired()
    {
        // The last day is still usable.
        Eval(Today.AddYears(-1), Today).Should().Be(AgreementState.ExpiringSoon);
    }

    [Fact]
    public void EndedYesterday_IsExpired()
    {
        Eval(Today.AddYears(-1), Today.AddDays(-1)).Should().Be(AgreementState.Expired);
    }

    [Fact]
    public void FutureStart_IsNotStarted()
    {
        Eval(Today.AddDays(10), Today.AddYears(1)).Should().Be(AgreementState.NotStarted);
    }

    [Fact]
    public void SuspendedStatus_WinsOverDates()
    {
        Eval(Today.AddYears(-1), Today.AddYears(1), PartnerLinkStatus.Suspended)
            .Should().Be(AgreementState.Suspended);
    }

    [Fact]
    public void ExpiredStatus_WinsOverValidDates()
    {
        Eval(Today.AddYears(-1), Today.AddYears(1), PartnerLinkStatus.Expired)
            .Should().Be(AgreementState.Expired);
    }

    [Theory]
    [InlineData(AgreementState.Active, true)]
    [InlineData(AgreementState.ExpiringSoon, true)]
    [InlineData(AgreementState.Expired, false)]
    [InlineData(AgreementState.Suspended, false)]
    [InlineData(AgreementState.NotStarted, false)]
    public void IsUsableForIntake_OnlyAllowsInForceAgreements(AgreementState state, bool usable)
    {
        PartnerAgreementRules.IsUsableForIntake(state).Should().Be(usable);
    }

    [Fact]
    public void DaysRemaining_IsNegativeOnceLapsed()
    {
        PartnerAgreementRules.DaysRemaining(Today.AddDays(-5), Today).Should().Be(-5);
        PartnerAgreementRules.DaysRemaining(Today.AddDays(30), Today).Should().Be(30);
    }

    [Fact]
    public void AgencyLevelCaps_MatchDirective()
    {
        // Arts. 18–22: partners per destination country / max licensed countries.
        AgencyLevelRules.GetCaps(1).Should().Be((20, (int?)null));
        AgencyLevelRules.GetCaps(2).Should().Be((20, (int?)8));
        AgencyLevelRules.GetCaps(3).Should().Be((16, (int?)8));
        AgencyLevelRules.GetCaps(4).Should().Be((8, (int?)4));
        AgencyLevelRules.GetCaps(5).Should().Be((4, (int?)2));
    }

    [Fact]
    public void Art40Caps_MatchDirective()
    {
        AgencyLevelRules.Art40MaxEthiopianAgencies(PartnerCapacityTier.Low).Should().Be(2);
        AgencyLevelRules.Art40MaxEthiopianAgencies(PartnerCapacityTier.Medium).Should().Be(4);
        AgencyLevelRules.Art40MaxEthiopianAgencies(PartnerCapacityTier.High).Should().Be(8);
    }
}
