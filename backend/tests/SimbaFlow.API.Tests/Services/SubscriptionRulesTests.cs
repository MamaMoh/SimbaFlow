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

    // ──── Invoice numbering ────
    //
    // The number was derived from a count of the year's invoices, and Number is uniquely indexed.
    // A count moves when a row is removed, so soft-deleting any invoice handed the next one a
    // number that already existed — and billing stopped, with the reason only in the Postgres log.

    [Fact]
    public void TheFirstInvoiceOfAYearIsNumberOne()
    {
        SubscriptionRules.NextInvoiceNumber(2026, Array.Empty<string>())
            .Should().Be("INV-2026-0001");
    }

    [Fact]
    public void NumberingFollowsTheHighestUsed_NotHowManySurvive()
    {
        // Every number the year has issued, including any since voided or soft-deleted — a
        // soft-deleted row is still a row, and the unique index on Number still holds it.
        string[] used = ["INV-2026-0001", "INV-2026-0002", "INV-2026-0003"];

        SubscriptionRules.NextInvoiceNumber(2026, used).Should().Be("INV-2026-0004");
    }

    [Fact]
    public void RemovingAnInvoiceDoesNotSendNumberingBackwards()
    {
        // The trap this replaced. Numbering came from a count of surviving rows, so voiding two of
        // three invoices made the next one 0002 — a number already taken, which the unique index
        // rejected. Billing then stopped for a reason visible only in the Postgres log.
        //
        // Expressed as a property: what the next number is must not depend on how many of the
        // year's invoices are still alive.
        string[] allThree = ["INV-2026-0001", "INV-2026-0002", "INV-2026-0003"];
        var countOfSurvivors = 1;

        SubscriptionRules.NextInvoiceNumber(2026, allThree)
            .Should().NotBe(SubscriptionRules.NextInvoiceNumber(2026, countOfSurvivors));
    }

    [Fact]
    public void NumberingIsScopedToItsYear()
    {
        string[] used = ["INV-2025-0001", "INV-2025-0002", "INV-2026-0001"];

        SubscriptionRules.NextInvoiceNumber(2026, used).Should().Be("INV-2026-0002");
        SubscriptionRules.NextInvoiceNumber(2027, used).Should().Be("INV-2027-0001",
            "a new year starts again at one");
    }

    [Fact]
    public void ANumberFromSomewhereElseDoesNotStopTheNextOne()
    {
        // Entered by hand, or carried over from whatever the agency used before.
        string[] used = ["INV-2026-0001", "2026/07/ACME", "", "INV-2026-nope"];

        SubscriptionRules.NextInvoiceNumber(2026, used).Should().Be("INV-2026-0002");
    }

    [Theory]
    [InlineData("INV-2026-0007", 2026, 7)]
    [InlineData("INV-2026-0007", 2025, 0)]
    [InlineData("INV-2026-0042", 2026, 42)]
    [InlineData(null, 2026, 0)]
    [InlineData("nonsense", 2026, 0)]
    public void ASequenceIsReadBackFromItsNumber(string? number, int year, int expected)
    {
        SubscriptionRules.SequenceOf(number, year).Should().Be(expected);
    }

    [Fact]
    public void NumbersStaySortableAsTheYearFillsUp()
    {
        // Zero padding is what keeps them in order in a list sorted as text.
        var ninth = SubscriptionRules.NextInvoiceNumber(2026, 8);
        var tenth = SubscriptionRules.NextInvoiceNumber(2026, 9);

        string.CompareOrdinal(ninth, tenth).Should().BeNegative();
    }
}
