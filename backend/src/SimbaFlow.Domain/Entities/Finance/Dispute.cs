using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Finance;

public class Dispute : BaseEntity
{
    public Guid CommissionId { get; set; }
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public string Reason { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public Guid OpenedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public Guid? ResolvedByUserId { get; set; }

    public Commission? Commission { get; set; }
}
