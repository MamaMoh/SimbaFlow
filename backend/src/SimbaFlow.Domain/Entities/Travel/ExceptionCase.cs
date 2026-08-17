using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Travel;

/// <summary>
/// Exception containment case opened when Arrival is flagged Returned or Runaway.
/// </summary>
public class ExceptionCase : BaseEntity
{
    public Guid CandidateId { get; set; }
    public ExceptionType Type { get; set; }
    public ExceptionStatus Status { get; set; } = ExceptionStatus.Open;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public Guid OpenedByUserId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ResolutionSummary { get; set; }
    public decimal? FinancialImpactAmount { get; set; }
    public string? FinancialImpactCurrency { get; set; }

    public ICollection<InvestigationNote> Notes { get; set; } = [];
    public ICollection<LiabilityAssignment> Liabilities { get; set; } = [];
}
