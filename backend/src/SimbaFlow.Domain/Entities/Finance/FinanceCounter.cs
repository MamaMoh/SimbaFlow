using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Finance;

/// <summary>
/// Single-row (per tenant schema) counter for journal entry and receipt numbers.
/// </summary>
public class FinanceCounter : BaseEntity
{
    public int NextJournalNumber { get; set; } = 1;
    public int NextReceiptNumber { get; set; } = 1;
}
