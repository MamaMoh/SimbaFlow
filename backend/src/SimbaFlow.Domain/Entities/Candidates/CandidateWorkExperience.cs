using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// One posting abroad: where, doing what, for how long.
///
/// A candidate used to have a single country and a single number of years, which fits a first
/// traveller and nobody else. Someone who worked two years in Lebanon and three in Kuwait had one
/// of those recorded and the other lost, and the partner agency reading the CV saw a shorter
/// career than the person actually had — which is the number the salary is argued from.
///
/// The candidate still carries <see cref="Candidate.WorksIn"/> and
/// <see cref="Candidate.ExperienceAbroadYears"/>, kept in step with this list whenever it is
/// saved. They are what the CV, the list page and the partner forms read, and a column the
/// database can sort and search beats a join it cannot.
/// </summary>
public class CandidateWorkExperience : BaseEntity
{
    public Guid CandidateId { get; set; }

    /// <summary>Country name as it is printed, not a code — the CV and the forms show this.</summary>
    public string Country { get; set; } = string.Empty;

    public string? Occupation { get; set; }

    /// <summary>Years in that posting. Null when the desk knows the country but not the term.</summary>
    public int? Years { get; set; }

    /// <summary>Keeps the rows in the order they were entered, which is the order they are read.</summary>
    public int SortOrder { get; set; }

    public Candidate? Candidate { get; set; }
}
