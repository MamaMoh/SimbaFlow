using SimbaFlow.Domain.Entities.Workflow;

namespace SimbaFlow.Infrastructure.Workflow;

/// <summary>
/// Core workflow engine service. Manages event-sourced state, transition evaluation,
/// and available action computation.
/// </summary>
public interface IWorkflowEngineService
{
    /// <summary>Derive current workflow state from event stream (with snapshot optimization).</summary>
    Task<WorkflowState> GetCurrentStateAsync(Guid candidateId, CancellationToken ct = default);

    /// <summary>Execute a workflow transition, appending an event and updating denormalized state.</summary>
    Task<TransitionResult> ExecuteTransitionAsync(
        Guid candidateId, Guid transitionRuleId, Guid userId, string userName, string? notes = null, CancellationToken ct = default);

    /// <summary>Update a status field within the current stage (e.g., medical status).</summary>
    Task<StatusUpdateResult> UpdateStatusAsync(
        Guid candidateId,
        string trackName,
        string newValue,
        Guid userId,
        string userName,
        string? notes = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool saveChanges = true,
        CancellationToken ct = default);

    /// <summary>
    /// Apply multiple status updates in one transaction (e.g. Insurance Paid → Available).
    /// Mirror views are evaluated once after the final change.
    /// </summary>
    Task<StatusUpdateResult> UpdateStatusChainAsync(
        Guid candidateId,
        IReadOnlyList<StatusChange> changes,
        Guid userId,
        string userName,
        CancellationToken ct = default);

    /// <summary>Compute available actions for a candidate given the user's roles.</summary>
    Task<List<AvailableAction>> GetAvailableActionsAsync(
        Guid candidateId, string[] userRoles, CancellationToken ct = default);
}

public class WorkflowState
{
    public Guid? StageId { get; set; }
    public string? StageName { get; set; }
    public Dictionary<string, string> StatusValues { get; set; } = new();
    public HashSet<Guid> VisibleInStages { get; set; } = [];

    public static WorkflowState Initial() => new();

    public static WorkflowState FromSnapshot(WorkflowSnapshot snapshot)
    {
        var state = new WorkflowState
        {
            StageId = snapshot.StageId,
            StageName = snapshot.StageName,
            VisibleInStages = [.. snapshot.VisibleInStages]
        };

        if (snapshot.StatusValues.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var prop in snapshot.StatusValues.RootElement.EnumerateObject())
            {
                state.StatusValues[prop.Name] = prop.Value.GetString() ?? "";
            }
        }

        return state;
    }

    public WorkflowState Apply(WorkflowEvent evt)
    {
        switch (evt.EventType)
        {
            case Domain.Enums.WorkflowEventType.Registered:
            case Domain.Enums.WorkflowEventType.StageTransitioned:
                StageId = evt.ToStageId;
                StageName = evt.ToStageName;
                break;

            case Domain.Enums.WorkflowEventType.StatusUpdated:
                if (evt.Data.RootElement.TryGetProperty("trackName", out var track) &&
                    evt.Data.RootElement.TryGetProperty("newValue", out var val))
                {
                    StatusValues[track.GetString()!] = val.GetString()!;
                }

                // Merge optional metadata keys into denormalized status bag
                foreach (var prop in evt.Data.RootElement.EnumerateObject())
                {
                    if (prop.Name is "trackName" or "oldValue" or "newValue")
                        continue;
                    if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                        StatusValues[prop.Name] = prop.Value.GetString() ?? "";
                    else if (prop.Value.ValueKind is System.Text.Json.JsonValueKind.Number
                             or System.Text.Json.JsonValueKind.True
                             or System.Text.Json.JsonValueKind.False)
                        StatusValues[prop.Name] = prop.Value.ToString();
                }
                break;

            case Domain.Enums.WorkflowEventType.MirrorViewActivated:
                if (evt.ToStageId.HasValue)
                    VisibleInStages.Add(evt.ToStageId.Value);
                break;

            case Domain.Enums.WorkflowEventType.MirrorViewDeactivated:
                if (evt.ToStageId.HasValue)
                    VisibleInStages.Remove(evt.ToStageId.Value);
                break;
        }

        return this;
    }
}

public record TransitionResult(bool IsSuccess, string? Error = null);
public record StatusUpdateResult(bool IsSuccess, string? Error = null);

public record StatusChange(
    string TrackName,
    string NewValue,
    string? Notes = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public record AvailableAction(
    Guid TransitionRuleId,
    /// <summary>Stage this transition leaves from — a board must only offer its own stage's steps.</summary>
    Guid SourceStageId,
    string ButtonLabel,
    string? ButtonIcon,
    bool IsEnabled,
    string? DisabledReason);
