using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Workflow;

/// <summary>
/// Root configuration entity for a tenant's workflow pipeline.
/// One active definition per tenant. Contains stages and transition rules.
/// </summary>
public class WorkflowDefinition : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "Default Workflow";
    public string? Description { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<WorkflowStage> Stages { get; set; } = [];
    public ICollection<WorkflowTransitionRule> TransitionRules { get; set; } = [];
}
