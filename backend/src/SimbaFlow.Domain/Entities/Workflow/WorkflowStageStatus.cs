using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Workflow;

/// <summary>
/// A possible status value within a workflow stage.
/// For parallel tracks, the TrackName identifies which track this status belongs to.
/// </summary>
public class WorkflowStageStatus : BaseEntity
{
    public Guid WorkflowStageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsTerminal { get; set; }
    public string? TrackName { get; set; }
    public string? Color { get; set; }

    // Navigation
    public WorkflowStage? WorkflowStage { get; set; }
}
