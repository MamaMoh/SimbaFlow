using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// Emergency / relative contact for a candidate.
/// </summary>
public class CandidateRelative : BaseEntity
{
    public Guid CandidateId { get; set; }
    public string RelativeName { get; set; } = string.Empty;
    public string? RelativePhone { get; set; }
    public string? RelativeKinship { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Region { get; set; }
    public string? City { get; set; }
    public string? Subcity { get; set; }
    public string? Woreda { get; set; }
    public string? HouseNo { get; set; }

    public Candidate? Candidate { get; set; }
}
