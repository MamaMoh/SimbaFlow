using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Workflow;

/// <summary>
/// Defines a candidate field that must have a value before a transition can execute.
/// If TransitionRuleId is null, the field is mandatory for ALL transitions from this stage.
/// </summary>
public class StageMandatoryField : BaseEntity
{
    public Guid WorkflowStageId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public Guid? TransitionRuleId { get; set; }

    // Navigation
    public WorkflowStage? WorkflowStage { get; set; }
    public WorkflowTransitionRule? TransitionRule { get; set; }
}
