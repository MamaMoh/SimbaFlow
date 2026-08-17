using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Finance;

/// <summary>
/// Single-row (per tenant schema) counter for journal entry numbers.
/// </summary>
public class FinanceCounter : BaseEntity
{
    public int NextJournalNumber { get; set; } = 1;
}
