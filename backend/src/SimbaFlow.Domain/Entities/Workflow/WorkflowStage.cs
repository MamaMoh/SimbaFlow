using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Workflow;

/// <summary>
/// A stage in the workflow pipeline (e.g., "Embassy", "LMIS", "Ticket").
/// Contains statuses, parallel tracks, and mirror view rules.
/// </summary>
public class WorkflowStage : BaseEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public StageType StageType { get; set; } = StageType.Simple;
    public bool IsInitialStage { get; set; }
    public bool IsFinalStage { get; set; }

    // Navigation
    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public ICollection<WorkflowStageStatus> Statuses { get; set; } = [];
    public ICollection<ParallelTrackDefinition> ParallelTracks { get; set; } = [];
    public ICollection<MirrorViewRule> MirrorViewRules { get; set; } = [];
    public ICollection<StageMandatoryField> MandatoryFields { get; set; } = [];
}
