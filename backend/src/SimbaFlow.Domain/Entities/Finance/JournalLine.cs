using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Finance;

public class JournalLine : BaseEntity
{
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Memo { get; set; }

    public JournalEntry? JournalEntry { get; set; }
    public Account? Account { get; set; }
}
