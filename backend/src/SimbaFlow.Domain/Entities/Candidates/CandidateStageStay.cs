namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// One visit of a candidate to a workflow stage (enter/exit/duration).
/// </summary>
public class CandidateStageStay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    public Guid StageId { get; set; }
    public string StageName { get; set; } = string.Empty;

    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExitedAt { get; set; }
    public long? DurationMs { get; set; }

    public Guid? EnteredByUserId { get; set; }
    public string? EnteredByUserName { get; set; }
    public Guid? ExitedByUserId { get; set; }
    public string? ExitedByUserName { get; set; }

    public string? ExitReason { get; set; }
    public Guid? EnterEventId { get; set; }
    public Guid? ExitEventId { get; set; }
    public bool IsCurrent { get; set; }

    public Candidate? Candidate { get; set; }
}
