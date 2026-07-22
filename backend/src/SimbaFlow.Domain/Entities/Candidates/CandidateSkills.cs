using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// 1:1 skills and experience profile for CV generation.
/// </summary>
public class CandidateSkills : BaseEntity
{
    public Guid CandidateId { get; set; }

    public string? EnglishLevel { get; set; }
    public string? ArabicLevel { get; set; }
    public string? ExperienceAbroad { get; set; }
    public short? ChildrenCount { get; set; }
    public decimal? Height { get; set; }
    public decimal? Weight { get; set; }
    public string? CookingNotes { get; set; }

    public bool CanIron { get; set; }
    public bool CanSew { get; set; }
    public bool CanBabysit { get; set; }
    public bool CanChildcare { get; set; }
    public bool CanArabicCooking { get; set; }
    public bool CanClean { get; set; }
    public bool CanWash { get; set; }
    public bool CanCook { get; set; }

    public Candidate? Candidate { get; set; }
}
