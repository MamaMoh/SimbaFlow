using System.Text.Json;

namespace SimbaFlow.Domain.Entities.Workflow;

/// <summary>
/// Periodic snapshot of a candidate's workflow state for replay performance.
/// Created every 20 events to limit replay to max 20 events.
/// </summary>
public class WorkflowSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    public long SequenceNumber { get; set; }
    public Guid StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public JsonDocument StatusValues { get; set; } = JsonDocument.Parse("{}");
    public Guid[] VisibleInStages { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
