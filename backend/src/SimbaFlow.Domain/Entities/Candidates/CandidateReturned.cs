using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// Return / containment details when arrival status is RETURNED.
/// </summary>
public class CandidateReturned : BaseEntity
{
    public Guid CandidateId { get; set; }
    public string? ReturnReason { get; set; }
    public DateOnly? ReturnDate { get; set; }
    public string? ReturnTicketInfo { get; set; }
    public Guid? CreatedByStaffId { get; set; }

    public Candidate? Candidate { get; set; }
}
