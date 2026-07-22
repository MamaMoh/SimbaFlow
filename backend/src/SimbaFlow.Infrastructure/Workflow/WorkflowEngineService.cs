using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.RealTime;

namespace SimbaFlow.Infrastructure.Workflow;

public class WorkflowEngineService : IWorkflowEngineService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISignalRBroadcaster? _broadcaster;
    private readonly ITenantContext _tenantContext;

    public WorkflowEngineService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ITenantContext tenantContext,
        ISignalRBroadcaster? broadcaster = null)
    {
        _db = db;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _broadcaster = broadcaster;
    }

    public async Task<Result> InitializeCandidateAsync(Candidate candidate, CancellationToken cancellationToken = default)
    {
        if (candidate.CurrentStageId is null)
            return Result.Success();

        var now = DateTime.UtcNow;
        var userId = Guid.TryParse(_currentUser.UserId, out var uid) ? uid : Guid.Empty;
        var userName = _currentUser.UserName ?? "system";

        var seq = await NextSequenceAsync(candidate.Id, cancellationToken);
        var evt = new WorkflowEvent
        {
            CandidateId = candidate.Id,
            SequenceNumber = seq,
            EventType = WorkflowEventType.Registered,
            ToStageId = candidate.CurrentStageId,
            ToStageName = candidate.CurrentStageName,
            UserId = userId,
            UserName = userName,
            Timestamp = now,
            Data = JsonDocument.Parse("{}")
        };
        _db.WorkflowEvents.Add(evt);

        _db.CandidateStageStays.Add(new CandidateStageStay
        {
            CandidateId = candidate.Id,
            StageId = candidate.CurrentStageId.Value,
            StageName = candidate.CurrentStageName ?? "",
            EnteredAt = now,
            EnteredByUserId = userId,
            EnteredByUserName = userName,
            EnterEventId = evt.Id,
            IsCurrent = true
        });

        candidate.CurrentStageEnteredAt = now;
        candidate.LastActionAt = now;
        candidate.LastActionLabel = "Registered";
        candidate.CurrentStatusValues ??= JsonDocument.Parse("{}");

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ExecuteTransitionAsync(
        Guid candidateId,
        Guid transitionRuleId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, cancellationToken);
        if (candidate is null)
            return Result.Failure("Candidate not found.", 404);

        var rule = await _db.WorkflowTransitionRules
            .Include(r => r.SourceStage)
            .Include(r => r.TargetStage)
            .FirstOrDefaultAsync(r => r.Id == transitionRuleId && r.IsActive && !r.IsDeleted, cancellationToken);
        if (rule is null)
            return Result.Failure("Transition rule not found.", 404);

        if (candidate.CurrentStageId != rule.SourceStageId)
            return Result.Failure("Candidate is not in the source stage for this transition.");

        if (!EvaluateConditions(rule.Conditions, candidate.CurrentStatusValues))
            return Result.Failure("Transition conditions are not met.");

        var now = DateTime.UtcNow;
        var userId = Guid.TryParse(_currentUser.UserId, out var uid) ? uid : Guid.Empty;
        var userName = _currentUser.UserName ?? "system";

        var currentStay = await _db.CandidateStageStays
            .FirstOrDefaultAsync(s => s.CandidateId == candidateId && s.IsCurrent, cancellationToken);
        if (currentStay is not null)
        {
            currentStay.IsCurrent = false;
            currentStay.ExitedAt = now;
            currentStay.ExitedByUserId = userId;
            currentStay.ExitedByUserName = userName;
            currentStay.ExitReason = "Transitioned";
            currentStay.DurationMs = (long)(now - currentStay.EnteredAt).TotalMilliseconds;
        }

        var seq = await NextSequenceAsync(candidateId, cancellationToken);
        var evt = new WorkflowEvent
        {
            CandidateId = candidateId,
            SequenceNumber = seq,
            EventType = WorkflowEventType.StageTransitioned,
            FromStageId = rule.SourceStageId,
            FromStageName = rule.SourceStage?.Name,
            ToStageId = rule.TargetStageId,
            ToStageName = rule.TargetStage?.Name,
            UserId = userId,
            UserName = userName,
            Timestamp = now,
            Notes = notes,
            Data = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                previousStageEnteredAt = currentStay?.EnteredAt,
                previousStageDurationMs = currentStay?.DurationMs
            }))
        };
        _db.WorkflowEvents.Add(evt);

        if (currentStay is not null)
            currentStay.ExitEventId = evt.Id;

        if (rule.RemoveFromSource)
        {
            candidate.CurrentStageId = rule.TargetStageId;
            candidate.CurrentStageName = rule.TargetStage?.Name;
            candidate.CurrentStageEnteredAt = now;

            _db.CandidateStageStays.Add(new CandidateStageStay
            {
                CandidateId = candidateId,
                StageId = rule.TargetStageId,
                StageName = rule.TargetStage?.Name ?? "",
                EnteredAt = now,
                EnteredByUserId = userId,
                EnteredByUserName = userName,
                EnterEventId = evt.Id,
                IsCurrent = true
            });
        }
        else
        {
            // Parallel branch (e.g. Commission) — keep primary stage, add visibility
            var visible = candidate.VisibleInStages.ToList();
            if (!visible.Contains(rule.TargetStageId))
                visible.Add(rule.TargetStageId);
            candidate.VisibleInStages = visible.ToArray();
        }

        await RefreshMirrorViewsAsync(candidate, cancellationToken);

        candidate.LastActionAt = now;
        candidate.LastActionLabel = rule.ButtonLabel;
        UpdateOverdueFlag(candidate, rule.TargetStage);

        await _db.SaveChangesAsync(cancellationToken);
        await BroadcastAsync(candidate, "StageTransitioned", rule.SourceStage?.Name, rule.TargetStage?.Name);
        return Result.Success();
    }

    public async Task<Result> UpdateStatusAsync(
        Guid candidateId,
        string trackKey,
        string newValue,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, cancellationToken);
        if (candidate is null)
            return Result.Failure("Candidate not found.", 404);

        var permission = await _db.StatusTransitionPermissions
            .FirstOrDefaultAsync(p => p.TrackKey == trackKey && p.ToStatus == newValue && !p.IsDeleted, cancellationToken);
        if (permission is not null)
        {
            var roles = _currentUser.Roles ?? [];
            if (!roles.Contains(permission.AllowedRoleCode, StringComparer.OrdinalIgnoreCase)
                && !_currentUser.IsSuperAdmin)
            {
                return Result.Failure($"Only {permission.AllowedRoleCode} can set {trackKey} to {newValue}.", 403);
            }
        }

        // Embassy status locked until medical=Fit and tasheer=Done
        if (trackKey.Equals("embassy", StringComparison.OrdinalIgnoreCase))
        {
            var statuses = ReadStatusMap(candidate.CurrentStatusValues);
            if (!string.Equals(statuses.GetValueOrDefault("medical"), "Fit", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(statuses.GetValueOrDefault("tasheer"), "Done", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure("Embassy status is locked until Medical=Fit and Tasheer=Done.");
            }
        }

        var now = DateTime.UtcNow;
        var userId = Guid.TryParse(_currentUser.UserId, out var uid) ? uid : Guid.Empty;
        var userName = _currentUser.UserName ?? "system";
        var map = ReadStatusMap(candidate.CurrentStatusValues);
        var oldValue = map.GetValueOrDefault(trackKey);

        var openStep = await _db.CandidateStepStays
            .Where(s => s.CandidateId == candidateId && s.TrackKey == trackKey && s.FinishedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        long? durationMs = null;
        if (openStep is not null)
        {
            openStep.FinishedAt = now;
            openStep.DurationMs = (long)(now - openStep.StartedAt).TotalMilliseconds;
            durationMs = openStep.DurationMs;
        }

        var currentStay = await _db.CandidateStageStays
            .FirstOrDefaultAsync(s => s.CandidateId == candidateId && s.IsCurrent, cancellationToken);

        var seq = await NextSequenceAsync(candidateId, cancellationToken);
        var evt = new WorkflowEvent
        {
            CandidateId = candidateId,
            SequenceNumber = seq,
            EventType = WorkflowEventType.StatusUpdated,
            FromStageId = candidate.CurrentStageId,
            FromStageName = candidate.CurrentStageName,
            ToStageId = candidate.CurrentStageId,
            ToStageName = candidate.CurrentStageName,
            UserId = userId,
            UserName = userName,
            Timestamp = now,
            Notes = notes,
            Data = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                track = trackKey,
                from = oldValue,
                to = newValue,
                stepStartedAt = openStep?.StartedAt,
                stepFinishedAt = now,
                durationMs
            }))
        };
        _db.WorkflowEvents.Add(evt);

        _db.CandidateStepStays.Add(new CandidateStepStay
        {
            CandidateId = candidateId,
            StageId = candidate.CurrentStageId ?? Guid.Empty,
            StageStayId = currentStay?.Id,
            TrackKey = trackKey,
            StatusValue = newValue,
            StartedAt = now,
            ChangedByUserId = userId,
            ChangedByUserName = userName,
            WorkflowEventId = evt.Id
        });

        map[trackKey] = newValue;
        candidate.CurrentStatusValues = JsonDocument.Parse(JsonSerializer.Serialize(map));
        candidate.LastActionAt = now;
        candidate.LastActionLabel = $"{trackKey} → {newValue}";

        if (trackKey.Equals("ticket", StringComparison.OrdinalIgnoreCase)
            && newValue.Equals("Booked", StringComparison.OrdinalIgnoreCase)
            && map.TryGetValue("flight_date", out var flightRaw)
            && DateTime.TryParse(flightRaw, out var flightDate))
        {
            candidate.FlightDate = flightDate.ToUniversalTime();
        }

        await RefreshMirrorViewsAsync(candidate, cancellationToken);

        var stage = candidate.CurrentStageId is null
            ? null
            : await _db.WorkflowStages.FirstOrDefaultAsync(s => s.Id == candidate.CurrentStageId, cancellationToken);
        UpdateOverdueFlag(candidate, stage);

        await _db.SaveChangesAsync(cancellationToken);
        await BroadcastAsync(candidate, "StatusUpdated", oldValue, newValue);
        return Result.Success();
    }

    public async Task<Result<object>> GetAvailableActionsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var candidate = await _db.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, cancellationToken);
        if (candidate is null)
            return Result<object>.Failure("Candidate not found.", 404);

        var rules = await _db.WorkflowTransitionRules
            .AsNoTracking()
            .Where(r => r.SourceStageId == candidate.CurrentStageId && r.IsActive && !r.IsDeleted)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(cancellationToken);

        var actions = rules.Select(r =>
        {
            var enabled = EvaluateConditions(r.Conditions, candidate.CurrentStatusValues);
            return new
            {
                transitionRuleId = r.Id,
                buttonLabel = r.ButtonLabel,
                buttonIcon = r.ButtonIcon,
                isEnabled = enabled,
                disabledReason = enabled ? null : "Conditions not met"
            };
        }).ToList();

        return Result<object>.Success(actions);
    }

    private async Task RefreshMirrorViewsAsync(Candidate candidate, CancellationToken cancellationToken)
    {
        if (candidate.CurrentStageId is null) return;

        var mirrors = await _db.MirrorViewRules
            .AsNoTracking()
            .Where(m => m.WorkflowStageId == candidate.CurrentStageId && m.IsActive && !m.IsDeleted)
            .ToListAsync(cancellationToken);

        var visible = new HashSet<Guid>(candidate.VisibleInStages);
        foreach (var mirror in mirrors)
        {
            if (EvaluateConditions(mirror.Conditions, candidate.CurrentStatusValues))
                visible.Add(mirror.TargetStageId);
            else
                visible.Remove(mirror.TargetStageId);
        }

        candidate.VisibleInStages = visible.ToArray();
    }

    private static void UpdateOverdueFlag(Candidate candidate, WorkflowStage? stage)
    {
        if (stage?.ExpectedDurationHours is null || candidate.CurrentStageEnteredAt is null)
        {
            candidate.IsOverdue = false;
            return;
        }

        var deadline = candidate.CurrentStageEnteredAt.Value.AddHours(stage.ExpectedDurationHours.Value);
        candidate.IsOverdue = DateTime.UtcNow > deadline;
    }

    private static bool EvaluateConditions(JsonDocument conditions, JsonDocument? statusValues)
    {
        if (conditions.RootElement.ValueKind != JsonValueKind.Object)
            return true;

        if (!conditions.RootElement.TryGetProperty("rules", out var rulesEl)
            || rulesEl.ValueKind != JsonValueKind.Array
            || rulesEl.GetArrayLength() == 0)
            return true;

        var map = ReadStatusMap(statusValues);
        var op = conditions.RootElement.TryGetProperty("operator", out var opEl)
            ? opEl.GetString() ?? "AND"
            : "AND";

        var results = new List<bool>();
        foreach (var rule in rulesEl.EnumerateArray())
        {
            var field = rule.GetProperty("field").GetString() ?? "";
            var cmp = rule.TryGetProperty("op", out var cmpEl) ? cmpEl.GetString() ?? "eq" : "eq";
            var expected = rule.TryGetProperty("value", out var valEl) ? valEl.GetString() : null;
            map.TryGetValue(field, out var actual);

            results.Add(cmp switch
            {
                "eq" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                "neq" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                "not_empty" => !string.IsNullOrWhiteSpace(actual),
                "empty" => string.IsNullOrWhiteSpace(actual),
                _ => false
            });
        }

        return op.Equals("OR", StringComparison.OrdinalIgnoreCase)
            ? results.Any(r => r)
            : results.All(r => r);
    }

    private static Dictionary<string, string> ReadStatusMap(JsonDocument? doc)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object)
            return map;

        foreach (var prop in doc.RootElement.EnumerateObject())
            map[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString() ?? ""
                : prop.Value.ToString();
        return map;
    }

    private async Task<long> NextSequenceAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        var max = await _db.WorkflowEvents
            .Where(e => e.CandidateId == candidateId)
            .Select(e => (long?)e.SequenceNumber)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    private async Task BroadcastAsync(Candidate candidate, string changeType, string? oldValue, string? newValue)
    {
        if (_broadcaster is null || _tenantContext.TenantId is null) return;
        await _broadcaster.BroadcastCandidateUpdateAsync(
            _tenantContext.TenantId.Value,
            candidate.OfficeId,
            new CandidateUpdatedMessage(
                candidate.Id,
                changeType,
                null,
                oldValue,
                newValue,
                _currentUser.UserName ?? "system",
                DateTime.UtcNow));
    }
}
