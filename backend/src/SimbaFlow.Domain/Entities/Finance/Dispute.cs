using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Finance;

public class Dispute : BaseEntity
{
    public Guid CommissionId { get; set; }
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public string Reason { get; set; } = string.Empty;

    /// <summary>The amount in dispute (ETB), when the dispute is about part of the balance.</summary>
    public decimal? DisputedAmountEtb { get; set; }

    /// <summary>Set on resolution.</summary>
    public DisputeOutcome? Outcome { get; set; }

    /// <summary>What resolving the dispute wrote off the balance (ETB); 0 when enforced.</summary>
    public decimal AdjustmentAmountEtb { get; set; }
    public Guid? AdjustmentJournalEntryId { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public Guid OpenedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public Guid? ResolvedByUserId { get; set; }

    public Commission? Commission { get; set; }
}
