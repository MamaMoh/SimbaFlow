using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// An extra skill on a candidate that is not one of the built-in boolean columns.
/// Built-in skills (Cleaning, Washing, …) stay on Candidate.SkillCleaning etc.
/// </summary>
public class CandidateSkill : BaseEntity
{
    public Guid CandidateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = true;

    public Candidate? Candidate { get; set; }
}
