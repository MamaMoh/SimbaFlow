using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Services;

/// <summary>How close an agency is to its next payment.</summary>
public enum RenewalNotice
{
    /// <summary>Nothing scheduled, or still far off. Say nothing.</summary>
    None = 0,

    /// <summary>Within the warning window. A quiet reminder.</summary>
    DueSoon = 1,

    /// <summary>Due today.</summary>
    DueToday = 2,

    /// <summary>The date has passed and it is still not paid.</summary>
    Overdue = 3,
}

/// <summary>
/// When an agency is warned about renewal, and what a billing period covers.
///
/// Kept out of the handlers because getting it wrong either nags an agency that has paid or
/// locks out one that has — both of which reach a paying customer before they reach us.
/// </summary>
public static class SubscriptionRules
{
    /// <summary>
    /// How long before the due date the warning starts.
    ///
    /// Two weeks for a yearly bill, a week for a monthly one: a yearly renewal is a larger sum
    /// that someone has to arrange, while a month's notice on a monthly bill would mean the
    /// banner is never absent.
    /// </summary>
    public static int WarningDays(BillingCycle cycle) => cycle == BillingCycle.Yearly ? 14 : 7;

    public static RenewalNotice NoticeFor(DateOnly? nextDue, BillingCycle cycle, DateOnly today)
    {
        if (nextDue is not DateOnly due) return RenewalNotice.None;

        var days = due.DayNumber - today.DayNumber;
        if (days < 0) return RenewalNotice.Overdue;
        if (days == 0) return RenewalNotice.DueToday;
        return days <= WarningDays(cycle) ? RenewalNotice.DueSoon : RenewalNotice.None;
    }

    /// <summary>Days until the payment is due; negative once it has passed.</summary>
    public static int? DaysUntilDue(DateOnly? nextDue, DateOnly today) =>
        nextDue is DateOnly due ? due.DayNumber - today.DayNumber : null;

    /// <summary>
    /// The period a bill raised on <paramref name="start"/> covers.
    ///
    /// Ends the day before the next period begins, so consecutive invoices meet without
    /// overlapping — a month billed on the 1st runs to the 31st, not into the next month.
    /// </summary>
    public static (DateOnly Start, DateOnly End) PeriodFor(DateOnly start, BillingCycle cycle)
    {
        var next = Advance(start, cycle);
        return (start, next.AddDays(-1));
    }

    /// <summary>
    /// One cycle on from a date.
    ///
    /// AddMonths clamps a 31st to the end of a shorter month, which is what we want for billing:
    /// an agency billed on the 31st is billed on the 30th in November, not on the 1st of December.
    /// </summary>
    public static DateOnly Advance(DateOnly from, BillingCycle cycle) =>
        cycle == BillingCycle.Yearly ? from.AddYears(1) : from.AddMonths(1);

    /// <summary>
    /// Whether this agency should be able to use the system.
    ///
    /// Being overdue does not itself lock anyone out — suspending is a decision somebody makes,
    /// not something a date does. An agency can be a week late and still working while the
    /// transfer clears.
    /// </summary>
    public static bool HasAccess(TenantStatus status) => status == TenantStatus.Active;

    /// <summary>
    /// The next invoice number for a year, given the highest sequence that year has already used.
    ///
    /// Year-scoped and zero-padded so invoices sort in the order they were raised, and so the
    /// numbering does not carry over into a new year.
    ///
    /// Takes the highest number used rather than a count of rows. A count moves when a row is
    /// removed: soft-delete any invoice and the next one is handed a number that already exists,
    /// which the unique index rejects — so billing stops working, on a Tuesday, for a reason
    /// nobody can see without reading the Postgres log.
    /// </summary>
    public static string NextInvoiceNumber(int year, int highestSequenceThisYear) =>
        $"INV-{year}-{highestSequenceThisYear + 1:D4}";

    /// <summary>
    /// The sequence number encoded in an invoice number, or 0 if it is not one of ours.
    ///
    /// Tolerant on purpose: a number entered by hand, or carried over from another system, should
    /// not stop the next invoice being raised.
    /// </summary>
    public static int SequenceOf(string? invoiceNumber, int year)
    {
        var prefix = $"INV-{year}-";
        if (string.IsNullOrWhiteSpace(invoiceNumber)
            || !invoiceNumber.StartsWith(prefix, StringComparison.Ordinal))
        {
            return 0;
        }

        return int.TryParse(invoiceNumber.AsSpan(prefix.Length), out var sequence) ? sequence : 0;
    }

    /// <summary>The next invoice number for a year, given every number that year has used.</summary>
    public static string NextInvoiceNumber(int year, IEnumerable<string?> existingNumbers)
    {
        var highest = existingNumbers
            .Select(n => SequenceOf(n, year))
            .DefaultIfEmpty(0)
            .Max();

        return NextInvoiceNumber(year, highest);
    }
}
