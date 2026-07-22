using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// Commission amount/metadata when a candidate is sent to the commission track.
/// Status values remain in Candidate.CurrentStatusValues["commission"].
/// </summary>
public class CandidateCommission : BaseEntity
{
    public Guid CandidateId { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public Guid? SentByStaffId { get; set; }
    public DateTime? SentAt { get; set; }

    public Candidate? Candidate { get; set; }
}
