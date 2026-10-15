using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Finance;

/// <summary>Money the agency paid out, posted Dr expense (or payable) / Cr cash.</summary>
public class Expense : BaseEntity
{
    public DateOnly ExpenseDate { get; set; }
    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Payee { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ETB";
    public decimal ExchangeRateToEtb { get; set; } = 1m;
    public decimal AmountEtb { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }

    /// <summary>The candidate's commission this was spent for (a medical, a ticket), if any.</summary>
    public Guid? CommissionId { get; set; }

    public Guid? JournalEntryId { get; set; }
    public Guid RecordedByUserId { get; set; }

    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
    public Guid? VoidJournalEntryId { get; set; }
}
