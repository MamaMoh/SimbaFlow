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

    public Commission? Commission { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
