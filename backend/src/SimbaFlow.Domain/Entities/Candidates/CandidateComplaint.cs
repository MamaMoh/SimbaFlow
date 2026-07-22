using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// Citizen grievance / complaint filed against a candidate.
/// </summary>
public class CandidateComplaint : BaseEntity
{
    public Guid CandidateId { get; set; }
    public string ComplaintText { get; set; } = string.Empty;
    public Guid? FiledByStaffId { get; set; }
    public string? FiledByUserName { get; set; }
    public string Status { get; set; } = "Open";

    public Candidate? Candidate { get; set; }
}
