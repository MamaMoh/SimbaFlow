using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Billing;

/// <summary>
/// One period's charge for an agency's subscription.
///
/// Lives in the platform schema rather than the agency's own: it records what the agency owes us,
/// and it has to stay readable when their schema is suspended for not paying it.
/// </summary>
public class SubscriptionInvoice : BaseEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Human-facing reference, e.g. INV-2026-0007.</summary>
    public string Number { get; set; } = string.Empty;

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public BillingCycle Cycle { get; set; }

    public DateOnly IssuedOn { get; set; }
    public DateOnly DueOn { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ETB";

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateOnly? PaidOn { get; set; }

    /// <summary>How it was paid — a bank reference, or a note from whoever recorded it.</summary>
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
}
