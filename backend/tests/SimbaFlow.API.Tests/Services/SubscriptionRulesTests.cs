using FluentAssertions;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// When an agency is warned, and what a bill covers.
///
/// Both directions of getting this wrong reach a paying customer before they reach us: nagging an
/// agency that has paid, or locking out one that has.
/// </summary>
public class SubscriptionRulesTests
{
    private static readonly DateOnly Today = new(2026, 9, 16);

    [Fact]
    public void NothingIsSaidWhenNoPaymentIsScheduled()
    {
        SubscriptionRules.NoticeFor(null, BillingCycle.Monthly, Today)
            .Should().Be(RenewalNotice.None, "an agency being onboarded has nothing to renew");
    }

    [Theory]
    [InlineData(30, RenewalNotice.None)]
    [InlineData(8, RenewalNotice.None)]
    [InlineData(7, RenewalNotice.DueSoon)]
    [InlineData(1, RenewalNotice.DueSoon)]
    [InlineData(0, RenewalNotice.DueToday)]
    [InlineData(-1, RenewalNotice.Overdue)]
    [InlineData(-90, RenewalNotice.Overdue)]
    public void AMonthlyAgencyIsWarnedInTheLastWeek(int daysAway, RenewalNotice expected)
    {
        var due = Today.AddDays(daysAway);
        SubscriptionRules.NoticeFor(due, BillingCycle.Monthly, Today).Should().Be(expected);
    }

    [Fact]
    public void AYearlyAgencyIsWarnedEarlierThanAMonthlyOne()
    {
        var due = Today.AddDays(10);

        SubscriptionRules.NoticeFor(due, BillingCycle.Yearly, Today)
            .Should().Be(RenewalNotice.DueSoon, "a year's fee takes arranging");
        SubscriptionRules.NoticeFor(due, BillingCycle.Monthly, Today)
            .Should().Be(RenewalNotice.None, "a month's notice on a monthly bill is always-on");
    }

    [Fact]
    public void ConsecutiveMonthlyPeriodsMeetWithoutOverlapping()
    {
        var first = SubscriptionRules.PeriodFor(new DateOnly(2026, 1, 1), BillingCycle.Monthly);
        var second = SubscriptionRules.PeriodFor(new DateOnly(2026, 2, 1), BillingCycle.Monthly);

        first.End.Should().Be(new DateOnly(2026, 1, 31));
        second.Start.Should().Be(first.End.AddDays(1));
    }

    [Fact]
    public void AYearlyPeriodRunsToTheDayBeforeTheAnniversary()
    {
        var period = SubscriptionRules.PeriodFor(new DateOnly(2026, 3, 1), BillingCycle.Yearly);

        period.End.Should().Be(new DateOnly(2027, 2, 28));
    }

    [Fact]
    public void BillingOnThe31stClampsToShorterMonths()
    {
        // Not the 1st of December: an agency billed on the 31st is billed at the end of the
        // month, and rolling forward would quietly move them into the next period.
        SubscriptionRules.Advance(new DateOnly(2026, 10, 31), BillingCycle.Monthly)
            .Should().Be(new DateOnly(2026, 11, 30));
    }

    [Fact]
    public void LeapDayDoesNotBreakAYearlyRenewal()
    {
        SubscriptionRules.Advance(new DateOnly(2028, 2, 29), BillingCycle.Yearly)
            .Should().Be(new DateOnly(2029, 2, 28));
    }

    [Theory]
    [InlineData(TenantStatus.Active, true)]
    [InlineData(TenantStatus.Suspended, false)]
    [InlineData(TenantStatus.Deactivated, false)]
    public void OnlyAnActiveAgencyCanUseTheSystem(TenantStatus status, bool allowed)
    {
        SubscriptionRules.HasAccess(status).Should().Be(allowed);
    }

    [Fact]
    public void BeingOverdueDoesNotByItselfLockAnAgencyOut()
    {
        // Suspending is a decision somebody makes. An agency can be a week late and still
        // working while the transfer clears — the alternative locks out customers who have paid.
        var overdue = SubscriptionRules.NoticeFor(Today.AddDays(-7), BillingCycle.Monthly, Today);

        overdue.Should().Be(RenewalNotice.Overdue);
        SubscriptionRules.HasAccess(TenantStatus.Active).Should().BeTrue();
    }

    [Fact]
    public void InvoiceNumbersAreYearScopedAndSortable()
    {
        SubscriptionRules.NextInvoiceNumber(2026, 0).Should().Be("INV-2026-0001");
        SubscriptionRules.NextInvoiceNumber(2026, 9).Should().Be("INV-2026-0010");
        SubscriptionRules.NextInvoiceNumber(2027, 0).Should().Be("INV-2027-0001",
            "the count restarts with the year");
    }
}
