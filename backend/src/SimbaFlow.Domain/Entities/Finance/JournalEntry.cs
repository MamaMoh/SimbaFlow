using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Finance;

public class JournalEntry : BaseEntity
{
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public Guid PostedByUserId { get; set; }

    public ICollection<JournalLine> Lines { get; set; } = [];
}
