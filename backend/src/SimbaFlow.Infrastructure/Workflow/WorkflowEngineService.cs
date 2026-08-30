using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Events;

namespace SimbaFlow.Infrastructure.Workflow;

/// <summary>
/// Event-sourced workflow engine. Derives state from snapshots + events,
/// executes transitions, updates status tracks, and evaluates mirror views.
/// </summary>
public class WorkflowEngineService : IWorkflowEngineService
{
    private const int SnapshotInterval = 20;

    private readonly ITenantDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public WorkflowEngineService(ITenantDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<WorkflowState> GetCurrentStateAsync(Guid candidateId, CancellationToken ct = default)
    {
        var snapshot = await _context.WorkflowSnapshots
            .AsNoTracking()
            .Where(s => s.CandidateId == candidateId)
            .OrderByDescending(s => s.SequenceNumber)
            .FirstOrDefaultAsync(ct);

        var state = snapshot is not null
            ? WorkflowState.FromSnapshot(snapshot)
            : WorkflowState.Initial();

        var fromSeq = snapshot?.SequenceNumber ?? 0;

        var events = await _context.WorkflowEvents
            .AsNoTracking()
            .Where(e => e.CandidateId == candidateId && e.SequenceNumber > fromSeq)
            .OrderBy(e => e.SequenceNumber)
            .ToListAsync(ct);

        // Include pending (unsaved) events from the same DbContext — needed for UpdateStatusChainAsync
        var pending = _context.WorkflowEvents.Local
            .Where(e => e.CandidateId == candidateId && e.SequenceNumber > fromSeq)
            .OrderBy(e => e.SequenceNumber)
            .ToList();

        foreach (var evt in events.Concat(pending).OrderBy(e => e.SequenceNumber).DistinctBy(e => e.SequenceNumber))
            state.Apply(evt);

        // Fill gaps from denormalized candidate fields (e.g. status-only event streams
        // never set StageId via Apply, but Candidate.CurrentStageId is authoritative).
        var candidate = await _context.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, ct);

        if (candidate is not null)
        {
            if (!state.StageId.HasValue)
            {
                state.StageId = candidate.CurrentStageId;
                state.StageName = candidate.CurrentStageName;
            }

            // Board membership is not fully derivable from the stream. Entering and leaving a
            // stage are both recorded as StageTransitioned, which carries no visibility payload,
            // and a snapshot freezes whatever set happened to be current when it was written. So
            // unioning replay with the column could only ever grow the set: a candidate kept every
            // board they had ever appeared on, and the next status update wrote that back. Both
            // write paths maintain this column on every mutation, so it is the authority; replay
            // supplies the parts it does not hold. Fall back to replay only for rows that predate
            // it being populated.
            if (candidate.VisibleInStages.Length > 0)
                state.VisibleInStages = [.. candidate.VisibleInStages];

            if (state.StatusValues.Count == 0 &&
                candidate.CurrentStatusValues is not null &&
                candidate.CurrentStatusValues.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in candidate.CurrentStatusValues.RootElement.EnumerateObject())
                    state.StatusValues[prop.Name] = prop.Value.GetString() ?? "";
            }
        }

        return state;
    }

    public async Task<TransitionResult> ExecuteTransitionAsync(
        Guid candidateId, Guid transitionRuleId, Guid userId, string userName,
        string? notes = null, CancellationToken ct = default)
    {
        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, ct);

        if (candidate is null)
            return new TransitionResult(false, "Candidate not found.");

        var rule = await _context.WorkflowTransitionRules
            .Include(r => r.SourceStage)
            .Include(r => r.TargetStage)
            .FirstOrDefaultAsync(r => r.Id == transitionRuleId && r.IsActive && !r.IsDeleted, ct);

        if (rule is null)
            return new TransitionResult(false, "Transition rule not found or inactive.");

        var state = await GetCurrentStateAsync(candidateId, ct);
        var candidateFields = BuildCandidateFields(candidate);

        // Validate: candidate must be in source stage (or visible via mirror)
        var inSource = state.StageId == rule.SourceStageId
            || state.VisibleInStages.Contains(rule.SourceStageId)
            || candidate.CurrentStageId == rule.SourceStageId;

        if (!inSource)
            return new TransitionResult(false,
                $"Candidate is not in the source stage '{rule.SourceStage?.Name ?? rule.SourceStageId.ToString()}'.");

        // Role check — empty AllowedRoles means any authenticated role
        if (rule.AllowedRoles.Length > 0)
        {
            var userRoles = _currentUser.Roles;
            if (!rule.AllowedRoles.Any(r => userRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
                return new TransitionResult(false, "You do not have a role permitted to execute this transition.");
        }

        if (!ConditionEvaluator.Evaluate(rule.Conditions, state.StatusValues, candidateFields))
            return new TransitionResult(false, "Transition conditions are not met.");

        var missing = GetMissingRequiredFields(rule.RequiredFields, candidateFields, state.StatusValues);
        if (missing.Count > 0)
            return new TransitionResult(false, $"Required fields missing: {string.Join(", ", missing)}.");

        var targetStage = rule.TargetStage
            ?? await _context.WorkflowStages.FirstOrDefaultAsync(s => s.Id == rule.TargetStageId, ct);

        if (targetStage is null)
            return new TransitionResult(false, "Target stage not found.");

        var nextSeq = await NextSequenceAsync(candidateId, ct);
        var fromStageId = state.StageId ?? candidate.CurrentStageId;
        var fromStageName = state.StageName ?? candidate.CurrentStageName;

        var evt = new WorkflowEvent
        {
            CandidateId = candidateId,
            SequenceNumber = nextSeq,
            EventType = WorkflowEventType.StageTransitioned,
            FromStageId = fromStageId,
            FromStageName = fromStageName,
            ToStageId = targetStage.Id,
            ToStageName = targetStage.Name,
            Data = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                transitionRuleId = rule.Id,
                removeFromSource = rule.RemoveFromSource
            })),
            UserId = userId,
            UserName = userName,
            Notes = notes
        };

        _context.WorkflowEvents.Add(evt);

        // Update denormalized candidate state
        candidate.CurrentStageId = targetStage.Id;
        candidate.CurrentStageName = targetStage.Name;
        candidate.StageEnteredAt = DateTime.UtcNow;

        var visible = state.VisibleInStages.ToHashSet();
        if (rule.RemoveFromSource)
        {
            if (fromStageId.HasValue)
                visible.Remove(fromStageId.Value);
            visible.Remove(rule.SourceStageId);

            // Clear all mirror targets rooted at the source stage (e.g. Case Executive, LMIS preview)
            var sourceMirrorTargets = await _context.MirrorViewRules
                .AsNoTracking()
                .Where(r => r.WorkflowStageId == rule.SourceStageId && !r.IsDeleted)
                .Select(r => r.TargetStageId)
                .ToListAsync(ct);

            // Record each clearing as an event. GetCurrentStateAsync rebuilds visibility by
            // replaying the stream and then unions the stored column into it, so a removal that
            // exists only in the column is undone the moment anything reads the state back: the
            // candidate reappears on the board they just left, and the next status update writes
            // that stale set back. Only MirrorViewDeactivated takes a stage out of a replay.
            foreach (var mirrorTargetId in sourceMirrorTargets)
            {
                if (!visible.Remove(mirrorTargetId)) continue;

                var clearedName = await _context.WorkflowStages
                    .AsNoTracking()
                    .Where(s => s.Id == mirrorTargetId)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync(ct);

                _context.WorkflowEvents.Add(new WorkflowEvent
                {
                    CandidateId = candidate.Id,
                    SequenceNumber = ++nextSeq,
                    EventType = WorkflowEventType.MirrorViewDeactivated,
                    FromStageId = rule.SourceStageId,
                    FromStageName = fromStageName,
                    ToStageId = mirrorTargetId,
                    ToStageName = clearedName,
                    Data = JsonDocument.Parse(JsonSerializer.Serialize(
                        new { clearedBy = "transition", transitionRuleId = rule.Id })),
                    UserId = userId,
                    UserName = userName
                });
            }
        }
        else if (fromStageId.HasValue)
        {
            visible.Add(fromStageId.Value);
            visible.Add(rule.SourceStageId);
        }

        visible.Add(targetStage.Id);

        // Apply transition to in-memory state for mirror evaluation
        state.Apply(evt);
        state.VisibleInStages = visible;

        // After a full transfer, re-evaluate mirrors on the *new* primary stage only
        await EvaluateMirrorViewsAsync(candidate, state, userId, userName, ct);

        candidate.VisibleInStages = state.VisibleInStages.ToArray();
        SyncStatusValues(candidate, state);

        candidate.AddDomainEvent(new CandidateStageChangedEvent(
            candidate.Id,
            candidate.FullName,
            _currentUser.TenantId ?? Guid.Empty,
            fromStageId,
            fromStageName,
            targetStage.Id,
            targetStage.Name,
            userName));

        await MaybeSnapshotAsync(candidateId, nextSeq, state, ct);
        await _context.SaveChangesAsync(ct);

        return new TransitionResult(true);
    }

    public async Task<StatusUpdateResult> UpdateStatusAsync(
        Guid candidateId, string trackName, string newValue, Guid userId, string userName,
        string? notes = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool saveChanges = true,
        CancellationToken ct = default)
    {
        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, ct);

        if (candidate is null)
            return new StatusUpdateResult(false, "Candidate not found.");

        if (string.IsNullOrWhiteSpace(trackName))
            return new StatusUpdateResult(false, "Track name is required.");

        if (candidate.CurrentStageId is null)
            return new StatusUpdateResult(false, "Candidate has no current stage.");

        var stage = await _context.WorkflowStages
            .Include(s => s.Statuses)
            .Include(s => s.ParallelTracks)
            .FirstOrDefaultAsync(s => s.Id == candidate.CurrentStageId, ct);

        if (stage is null)
            return new StatusUpdateResult(false, "Current stage not found.");

        // Validate track if stage uses parallel tracks
        if (stage.StageType == StageType.ParallelTrack && stage.ParallelTracks.Count > 0)
        {
            var trackOk = stage.ParallelTracks.Any(t =>
                t.TrackName.Equals(trackName, StringComparison.OrdinalIgnoreCase) && !t.IsDeleted);
            if (!trackOk && !stage.Statuses.Any(s =>
                    string.Equals(s.TrackName, trackName, StringComparison.OrdinalIgnoreCase) ||
                    (s.TrackName is null && s.Name.Equals(newValue, StringComparison.OrdinalIgnoreCase))))
            {
                // Allow known status names mapped under a free-form field (e.g. visa)
                var statusOk = stage.Statuses.Any(s =>
                    !s.IsDeleted &&
                    (string.Equals(s.TrackName, trackName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(s.Name, newValue, StringComparison.OrdinalIgnoreCase)));
                if (!statusOk)
                    return new StatusUpdateResult(false, $"Unknown track '{trackName}' for stage '{stage.Name}'.");
            }
        }

        // Validate status value when statuses are defined for the track
        var allowedStatuses = stage.Statuses
            .Where(s => !s.IsDeleted &&
                        (s.TrackName is null ||
                         s.TrackName.Equals(trackName, StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.Name)
            .ToList();

        if (allowedStatuses.Count > 0 &&
            !allowedStatuses.Any(s => s.Equals(newValue, StringComparison.OrdinalIgnoreCase)))
        {
            return new StatusUpdateResult(false,
                $"Invalid status '{newValue}' for track '{trackName}'. Allowed: {string.Join(", ", allowedStatuses)}.");
        }

        var state = await GetCurrentStateAsync(candidateId, ct);
        state.StatusValues.TryGetValue(trackName, out var oldValue);

        var payload = new Dictionary<string, object?>
        {
            ["trackName"] = trackName,
            ["oldValue"] = oldValue,
            ["newValue"] = newValue
        };
        if (metadata is not null)
        {
            foreach (var (key, value) in metadata)
            {
                if (key is "trackName" or "oldValue" or "newValue")
                    continue;
                payload[key] = value;
            }
        }

        var nextSeq = await NextSequenceAsync(candidateId, ct);
        var evt = new WorkflowEvent
        {
            CandidateId = candidateId,
            SequenceNumber = nextSeq,
            EventType = WorkflowEventType.StatusUpdated,
            FromStageId = state.StageId,
            FromStageName = state.StageName,
            ToStageId = state.StageId,
            ToStageName = state.StageName,
            Data = JsonDocument.Parse(JsonSerializer.Serialize(payload)),
            UserId = userId,
            UserName = userName,
            Notes = notes
        };

        _context.WorkflowEvents.Add(evt);
        state.Apply(evt);

        await EvaluateMirrorViewsAsync(candidate, state, userId, userName, ct);

        candidate.VisibleInStages = state.VisibleInStages.ToArray();
        SyncStatusValues(candidate, state);

        candidate.AddDomainEvent(new CandidateStatusChangedEvent(
            candidate.Id,
            candidate.FullName,
            _currentUser.TenantId ?? Guid.Empty,
            trackName,
            oldValue,
            newValue,
            userName));

        await MaybeSnapshotAsync(candidateId, nextSeq, state, ct);

        if (saveChanges)
            await _context.SaveChangesAsync(ct);

        return new StatusUpdateResult(true);
    }

    public async Task<StatusUpdateResult> UpdateStatusChainAsync(
        Guid candidateId,
        IReadOnlyList<StatusChange> changes,
        Guid userId,
        string userName,
        CancellationToken ct = default)
    {
        if (changes.Count == 0)
            return new StatusUpdateResult(false, "At least one status change is required.");

        // Apply all but the last without saving; last call persists + evaluates mirrors once more
        // (each call also evaluates mirrors — final state is authoritative after last Apply).
        StatusUpdateResult? last = null;
        for (var i = 0; i < changes.Count; i++)
        {
            var change = changes[i];
            var isLast = i == changes.Count - 1;
            last = await UpdateStatusAsync(
                candidateId,
                change.TrackName,
                change.NewValue,
                userId,
                userName,
                change.Notes,
                change.Metadata,
                saveChanges: isLast,
                ct);

            if (!last.IsSuccess)
                return last;
        }

        return last ?? new StatusUpdateResult(false, "No changes applied.");
    }

    public async Task<List<AvailableAction>> GetAvailableActionsAsync(
        Guid candidateId, string[] userRoles, CancellationToken ct = default)
    {
        var candidate = await _context.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, ct);

        if (candidate is null)
            return [];

        var state = await GetCurrentStateAsync(candidateId, ct);
        var candidateFields = BuildCandidateFields(candidate);

        var stageIds = new HashSet<Guid>();
        if (state.StageId.HasValue)
            stageIds.Add(state.StageId.Value);
        foreach (var v in state.VisibleInStages)
            stageIds.Add(v);

        if (stageIds.Count == 0)
            return [];

        var rules = await _context.WorkflowTransitionRules
            .AsNoTracking()
            .Where(r => r.IsActive && !r.IsDeleted && stageIds.Contains(r.SourceStageId))
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var actions = new List<AvailableAction>();

        foreach (var rule in rules)
        {
            string? disabledReason = null;
            var enabled = true;

            // A role block is never actionable by this user, so hide the step entirely
            // rather than showing a button they can never press. (Condition/field blocks
            // stay visible-but-disabled because the user can still satisfy them.)
            if (rule.AllowedRoles.Length > 0 &&
                !rule.AllowedRoles.Any(r => userRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!ConditionEvaluator.TryEvaluate(
                    rule.Conditions, state.StatusValues, candidateFields, out var blockedBy))
            {
                enabled = false;
                disabledReason = blockedBy;
            }
            else
            {
                var missing = GetMissingRequiredFields(rule.RequiredFields, candidateFields, state.StatusValues);
                if (missing.Count > 0)
                {
                    enabled = false;
                    disabledReason = $"Missing: {string.Join(", ", missing)}";
                }
            }

            actions.Add(new AvailableAction(
                rule.Id,
                rule.SourceStageId,
                rule.ButtonLabel,
                rule.ButtonIcon,
                enabled,
                disabledReason));
        }

        return actions;
    }

    /// <summary>
    /// Append a Registered event and optional initial snapshot for a newly created candidate.
    /// Call after the candidate is added to the context (before or with SaveChanges).
    /// </summary>
    public async Task AppendRegisteredEventAsync(
        Candidate candidate, Guid userId, string userName, CancellationToken ct = default)
    {
        var evt = new WorkflowEvent
        {
            CandidateId = candidate.Id,
            SequenceNumber = 1,
            EventType = WorkflowEventType.Registered,
            ToStageId = candidate.CurrentStageId,
            ToStageName = candidate.CurrentStageName,
            Data = JsonDocument.Parse("{}"),
            UserId = userId,
            UserName = userName
        };

        _context.WorkflowEvents.Add(evt);

        if (candidate.CurrentStageId.HasValue && candidate.CurrentStageName is not null)
        {
            var state = WorkflowState.Initial();
            state.StageId = candidate.CurrentStageId;
            state.StageName = candidate.CurrentStageName;
            // No snapshot on seq 1 (interval is 20)
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Re-run mirror rules across the whole caseload. Used after an admin edits a mirror rule,
    /// since mirrors are otherwise only evaluated as a side effect of a status change.
    /// </summary>
    public async Task<int> ReapplyMirrorViewsAsync(
        Guid userId, string userName, CancellationToken ct = default)
    {
        var candidateIds = await _context.Candidates
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var changed = 0;

        foreach (var candidateId in candidateIds)
        {
            var candidate = await _context.Candidates
                .FirstOrDefaultAsync(c => c.Id == candidateId, ct);
            if (candidate is null)
                continue;

            var state = await GetCurrentStateAsync(candidateId, ct);
            if (!state.StageId.HasValue)
                continue;

            var before = new HashSet<Guid>(state.VisibleInStages);
            await EvaluateMirrorViewsAsync(candidate, state, userId, userName, ct);

            if (!before.SetEquals(state.VisibleInStages))
            {
                candidate.VisibleInStages = state.VisibleInStages.ToArray();
                changed++;
            }
        }

        if (changed > 0)
            await _context.SaveChangesAsync(ct);

        return changed;
    }

    private async Task EvaluateMirrorViewsAsync(
        Candidate candidate, WorkflowState state, Guid userId, string userName, CancellationToken ct)
    {
        if (!state.StageId.HasValue)
            return;

        // A candidate can sit on a board through a mirror rather than by being in that stage, and
        // the rules hanging off that board still have to run. Scoping this to the current stage
        // alone stranded anyone who reached Embassy as a mirror: their Embassy → LMIS rule was
        // never evaluated, so they could never appear on LMIS.
        var sourceStageIds = new HashSet<Guid> { state.StageId.Value };
        foreach (var visible in state.VisibleInStages)
            sourceStageIds.Add(visible);

        var rules = await _context.MirrorViewRules
            .Where(r => sourceStageIds.Contains(r.WorkflowStageId) && r.IsActive && !r.IsDeleted)
            .ToListAsync(ct);

        var candidateFields = BuildCandidateFields(candidate);
        var nextSeq = await NextSequenceAsync(candidate.Id, ct);

        foreach (var rule in rules)
        {
            var met = ConditionEvaluator.Evaluate(rule.Conditions, state.StatusValues, candidateFields);
            var alreadyVisible = state.VisibleInStages.Contains(rule.TargetStageId);

            if (met && !alreadyVisible)
            {
                var target = await _context.WorkflowStages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == rule.TargetStageId, ct);

                var evt = new WorkflowEvent
                {
                    CandidateId = candidate.Id,
                    SequenceNumber = nextSeq++,
                    EventType = WorkflowEventType.MirrorViewActivated,
                    FromStageId = state.StageId,
                    FromStageName = state.StageName,
                    ToStageId = rule.TargetStageId,
                    ToStageName = target?.Name,
                    Data = JsonDocument.Parse(JsonSerializer.Serialize(new { mirrorRuleId = rule.Id })),
                    UserId = userId,
                    UserName = userName
                };
                _context.WorkflowEvents.Add(evt);
                state.Apply(evt);
            }
            else if (!met && alreadyVisible)
            {
                var target = await _context.WorkflowStages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == rule.TargetStageId, ct);

                var evt = new WorkflowEvent
                {
                    CandidateId = candidate.Id,
                    SequenceNumber = nextSeq++,
                    EventType = WorkflowEventType.MirrorViewDeactivated,
                    FromStageId = state.StageId,
                    FromStageName = state.StageName,
                    ToStageId = rule.TargetStageId,
                    ToStageName = target?.Name,
                    Data = JsonDocument.Parse(JsonSerializer.Serialize(new { mirrorRuleId = rule.Id })),
                    UserId = userId,
                    UserName = userName
                };
                _context.WorkflowEvents.Add(evt);
                state.Apply(evt);
            }
        }
    }

    private async Task MaybeSnapshotAsync(Guid candidateId, long sequenceNumber, WorkflowState state, CancellationToken ct)
    {
        if (sequenceNumber % SnapshotInterval != 0 || !state.StageId.HasValue)
            return;

        _context.WorkflowSnapshots.Add(new WorkflowSnapshot
        {
            CandidateId = candidateId,
            SequenceNumber = sequenceNumber,
            StageId = state.StageId.Value,
            StageName = state.StageName ?? "",
            StatusValues = JsonDocument.Parse(JsonSerializer.Serialize(state.StatusValues)),
            VisibleInStages = state.VisibleInStages.ToArray()
        });

        await Task.CompletedTask;
    }

    private async Task<long> NextSequenceAsync(Guid candidateId, CancellationToken ct)
    {
        var maxDb = await _context.WorkflowEvents
            .Where(e => e.CandidateId == candidateId)
            .Select(e => (long?)e.SequenceNumber)
            .MaxAsync(ct) ?? 0;

        var maxTracked = _context.WorkflowEvents.Local
            .Where(e => e.CandidateId == candidateId)
            .Select(e => (long?)e.SequenceNumber)
            .DefaultIfEmpty(null)
            .Max() ?? 0;

        return Math.Max(maxDb, maxTracked) + 1;
    }

    private static void SyncStatusValues(Candidate candidate, WorkflowState state)
    {
        candidate.CurrentStatusValues = JsonDocument.Parse(JsonSerializer.Serialize(state.StatusValues));
    }

    private static Dictionary<string, string?> BuildCandidateFields(Candidate candidate)
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["firstName"] = candidate.FirstName,
            ["lastName"] = candidate.LastName,
            ["passportNumber"] = candidate.PassportNumber,
            ["labourId"] = candidate.LabourId,
            ["nationality"] = candidate.Nationality,
            ["phoneNumber"] = candidate.PhoneNumber,
            ["email"] = candidate.Email,
            ["countryOfTravel"] = candidate.CountryOfTravel,
            ["partnerName"] = candidate.PartnerName,
            ["contractDate"] = candidate.ContractDate?.ToString("yyyy-MM-dd"),
            ["ticket_status"] = null,
            ["destination"] = candidate.CountryOfTravel,
            ["flight_date"] = null,
        };
    }

    private static List<string> GetMissingRequiredFields(
        string[] requiredFields,
        Dictionary<string, string?> candidateFields,
        Dictionary<string, string> statusValues)
    {
        var missing = new List<string>();
        foreach (var field in requiredFields)
        {
            if (statusValues.TryGetValue(field, out var statusVal) && !string.IsNullOrWhiteSpace(statusVal))
                continue;

            if (candidateFields.TryGetValue(field, out var fieldVal) && !string.IsNullOrWhiteSpace(fieldVal))
                continue;

            // Also check status values under common aliases
            if (statusValues.Keys.Any(k => k.Equals(field, StringComparison.OrdinalIgnoreCase)
                                           && !string.IsNullOrWhiteSpace(statusValues[k])))
                continue;

            missing.Add(field);
        }

        return missing;
    }
}
