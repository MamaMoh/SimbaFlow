using System.Text.Json;
using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Workflow;

/// <summary>
/// When conditions are met, the candidate appears in both the source stage
/// and the target stage simultaneously (single record, multiple views).
/// </summary>
public class MirrorViewRule : BaseEntity
{
    public Guid WorkflowStageId { get; set; }
    public Guid TargetStageId { get; set; }

    /// <summary>
    /// Conditions that trigger mirror visibility.
    /// Same format as WorkflowTransitionRule.Conditions.
    /// </summary>
    public JsonDocument Conditions { get; set; } = JsonDocument.Parse("{}");

    public bool IsActive { get; set; } = true;

    // Navigation
    public WorkflowStage? WorkflowStage { get; set; }
    public WorkflowStage? TargetStage { get; set; }
}
