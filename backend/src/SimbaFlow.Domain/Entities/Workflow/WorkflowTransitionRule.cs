using System.Text.Json;
using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Workflow;

/// <summary>
/// Defines a transition between two workflow stages.
/// Includes conditions, required fields, allowed roles, and button configuration.
/// </summary>
public class WorkflowTransitionRule : BaseEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public Guid SourceStageId { get; set; }
    public Guid TargetStageId { get; set; }
    public string ButtonLabel { get; set; } = string.Empty;
    public string? ButtonIcon { get; set; }
    public int SortOrder { get; set; }

    /// <summary>
    /// Conditions that must evaluate to TRUE before transition can execute.
    /// Format: { "operator": "AND", "rules": [{ "field": "...", "op": "eq", "value": "..." }] }
    /// </summary>
    public JsonDocument Conditions { get; set; } = JsonDocument.Parse("{}");

    /// <summary>Candidate fields that must have non-empty values before transition.</summary>
    public string[] RequiredFields { get; set; } = [];

    /// <summary>User roles that are allowed to execute this transition.</summary>
    public string[] AllowedRoles { get; set; } = [];

    /// <summary>If false, candidate remains visible in source stage (mirror behavior).</summary>
    public bool RemoveFromSource { get; set; } = true;

    public bool IsActive { get; set; } = true;

    // Navigation
    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public WorkflowStage? SourceStage { get; set; }
    public WorkflowStage? TargetStage { get; set; }
}
