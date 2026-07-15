using System.Text.Json;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Workflow;

/// <summary>
/// Immutable event in the workflow event stream. Append-only — never updated or deleted.
/// The complete ordered sequence of events for a candidate defines their workflow history.
/// </summary>
public class WorkflowEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    public long SequenceNumber { get; set; }
    public WorkflowEventType EventType { get; set; }
    public Guid? FromStageId { get; set; }
    public string? FromStageName { get; set; }
    public Guid? ToStageId { get; set; }
    public string? ToStageName { get; set; }

    /// <summary>Event-specific payload stored as JSONB.</summary>
    public JsonDocument Data { get; set; } = JsonDocument.Parse("{}");

    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
