using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Workflow;

/// <summary>
/// Defines a parallel track within a stage (e.g., "Medical" and "Tasheer" in Embassy stage).
/// Each track has its own completion status that must be reached.
/// </summary>
public class ParallelTrackDefinition : BaseEntity
{
    public Guid WorkflowStageId { get; set; }
    public string TrackName { get; set; } = string.Empty;
    public string CompletionStatus { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    // Navigation
    public WorkflowStage? WorkflowStage { get; set; }
}
