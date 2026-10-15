using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Finance;

public class Payment : BaseEntity
{
    public Guid CommissionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ETB";
    public decimal ExchangeRateToEtb { get; set; } = 1m;
    public decimal AmountEtb { get; set; }
    public DateTime PaidAt { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid RecordedByUserId { get; set; }

    /// <summary>Printed on the receipt; numbered per agency in the order payments were taken.</summary>
    public string? ReceiptNumber { get; set; }

    /// <summary>
    /// Set when the payment was taken back (entered wrongly, bounced). A voided payment stays on
    /// record with its reversing journal entry, and no longer counts toward what was paid.
    /// </summary>
    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
    public Guid? VoidJournalEntryId { get; set; }

    public bool IsVoided => VoidedAt is not null;

    public Commission? Commission { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
