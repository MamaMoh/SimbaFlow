namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// Time spent on a specific status value within a track (e.g. medical=OnProgress).
/// </summary>
public class CandidateStepStay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    public Guid StageId { get; set; }
    public Guid? StageStayId { get; set; }

    public string TrackKey { get; set; } = string.Empty;
    public string StatusValue { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public long? DurationMs { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByUserName { get; set; }
    public Guid? WorkflowEventId { get; set; }

    public Candidate? Candidate { get; set; }
    public CandidateStageStay? StageStay { get; set; }
}
