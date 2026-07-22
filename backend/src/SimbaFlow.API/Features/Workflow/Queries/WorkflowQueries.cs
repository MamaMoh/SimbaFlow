using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Workflow.Queries;

public record GetAvailableActionsQuery(Guid CandidateId) : IRequest<Result<object>>;

public class GetAvailableActionsHandler : IRequestHandler<GetAvailableActionsQuery, Result<object>>
{
    private readonly IWorkflowEngineService _engine;

    public GetAvailableActionsHandler(IWorkflowEngineService engine) => _engine = engine;

    public Task<Result<object>> Handle(GetAvailableActionsQuery request, CancellationToken cancellationToken)
        => _engine.GetAvailableActionsAsync(request.CandidateId, cancellationToken);
}

public record GetWorkflowStateQuery(Guid CandidateId) : IRequest<Result<object>>;

public class GetWorkflowStateHandler : IRequestHandler<GetWorkflowStateQuery, Result<object>>
{
    private readonly IApplicationDbContext _db;

    public GetWorkflowStateHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<object>> Handle(GetWorkflowStateQuery request, CancellationToken cancellationToken)
    {
        var c = await _db.Candidates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CandidateId && !x.IsDeleted, cancellationToken);
        if (c is null) return Result<object>.Failure("Candidate not found.", 404);

        var days = c.CurrentStageEnteredAt.HasValue
            ? (int)(DateTime.UtcNow - c.CurrentStageEnteredAt.Value).TotalDays
            : 0;

        return Result<object>.Success(new
        {
            stageId = c.CurrentStageId,
            stageName = c.CurrentStageName,
            statusValues = c.CurrentStatusValues?.RootElement,
            visibleInStages = c.VisibleInStages,
            currentStageEnteredAt = c.CurrentStageEnteredAt,
            daysInStage = days,
            isOverdue = c.IsOverdue,
            lastActionAt = c.LastActionAt,
            lastActionLabel = c.LastActionLabel,
            flightDate = c.FlightDate
        });
    }
}

public record GetWorkflowEventsQuery(Guid CandidateId) : IRequest<Result<object>>;

public class GetWorkflowEventsHandler : IRequestHandler<GetWorkflowEventsQuery, Result<object>>
{
    private readonly IApplicationDbContext _db;

    public GetWorkflowEventsHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<object>> Handle(GetWorkflowEventsQuery request, CancellationToken cancellationToken)
    {
        var events = await _db.WorkflowEvents.AsNoTracking()
            .Where(e => e.CandidateId == request.CandidateId)
            .OrderByDescending(e => e.SequenceNumber)
            .Select(e => new
            {
                e.Id,
                eventType = (int)e.EventType,
                eventTypeName = e.EventType.ToString(),
                e.FromStageName,
                e.ToStageName,
                e.UserName,
                e.Timestamp,
                e.Notes,
                data = e.Data.RootElement
            })
            .ToListAsync(cancellationToken);

        return Result<object>.Success(events);
    }
}

public record GetViewCandidatesQuery(Guid StageId, int Page, int PageSize, string? Search, Guid? OfficeId) : IRequest<Result<object>>;

public class GetViewCandidatesHandler : IRequestHandler<GetViewCandidatesQuery, Result<object>>
{
    private readonly IApplicationDbContext _db;

    public GetViewCandidatesHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<object>> Handle(GetViewCandidatesQuery request, CancellationToken cancellationToken)
    {
        var stage = await _db.WorkflowStages.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StageId && !s.IsDeleted, cancellationToken);

        var query = _db.Candidates.AsNoTracking().Where(c => !c.IsDeleted);

        if (request.OfficeId.HasValue)
            query = query.Where(c => c.OfficeId == request.OfficeId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(s) ||
                c.LastName.ToLower().Contains(s) ||
                c.PassportNumber.ToLower().Contains(s) ||
                (c.LabourId != null && c.LabourId.ToLower().Contains(s)) ||
                c.ApplicationNo.ToLower().Contains(s));
        }

        // Materialize then filter by primary stage or mirror visibility
        // (VisibleInStages is JSON-converted; cannot translate Contains to SQL)
        var all = await query.OrderByDescending(c => c.LastActionAt).ToListAsync(cancellationToken);
        var filtered = all
            .Where(c => c.CurrentStageId == request.StageId || c.VisibleInStages.Contains(request.StageId))
            .ToList();

        var total = filtered.Count;
        var pageItems = filtered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new
            {
                c.Id,
                c.ApplicationNo,
                fullName = string.IsNullOrEmpty(c.MiddleName) ? $"{c.FirstName} {c.LastName}" : $"{c.FirstName} {c.MiddleName} {c.LastName}",
                c.PassportNumber,
                c.LabourId,
                c.CountryOfTravel,
                officeName = c.OfficeName,
                currentStatusValues = c.CurrentStatusValues?.RootElement,
                enteredAt = c.CurrentStageEnteredAt,
                daysInStage = c.CurrentStageEnteredAt == null ? 0 : (int)(DateTime.UtcNow - c.CurrentStageEnteredAt.Value).TotalDays,
                c.LastActionAt,
                c.LastActionLabel,
                c.IsOverdue,
                isPreview = c.CurrentStageId != request.StageId,
                c.FlightDate,
                remainingDays = c.FlightDate == null ? (int?)null : (int)(c.FlightDate.Value.Date - DateTime.UtcNow.Date).TotalDays,
                availableActions = Array.Empty<object>()
            })
            .ToList();

        return Result<object>.Success(new
        {
            stageId = request.StageId,
            stageName = stage?.Name ?? request.StageId.ToString(),
            items = pageItems,
            totalCount = total,
            page = request.Page,
            pageSize = request.PageSize
        });
    }
}

public record GetWorkflowDefinitionQuery : IRequest<Result<object>>;

public class GetWorkflowDefinitionHandler : IRequestHandler<GetWorkflowDefinitionQuery, Result<object>>
{
    private readonly IApplicationDbContext _db;

    public GetWorkflowDefinitionHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<object>> Handle(GetWorkflowDefinitionQuery request, CancellationToken cancellationToken)
    {
        var def = await _db.WorkflowDefinitions.AsNoTracking()
            .Include(d => d.Stages).ThenInclude(s => s.Statuses)
            .Include(d => d.Stages).ThenInclude(s => s.ParallelTracks)
            .Include(d => d.TransitionRules)
            .FirstOrDefaultAsync(d => d.IsActive && !d.IsDeleted, cancellationToken);

        if (def is null)
            return Result<object>.Failure("No active workflow definition.", 404);

        return Result<object>.Success(new
        {
            def.Id,
            def.Name,
            def.Description,
            def.Version,
            def.IsActive,
            stages = def.Stages.Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).Select(s => new
            {
                s.Id,
                s.Name,
                s.Description,
                s.SortOrder,
                stageType = (int)s.StageType,
                s.IsInitialStage,
                s.IsFinalStage,
                s.ExpectedDurationHours,
                s.WarningDurationHours,
                s.CriticalDurationHours,
                statuses = s.Statuses.Where(x => !x.IsDeleted).OrderBy(x => x.SortOrder).Select(x => new
                {
                    x.Id, x.Name, x.SortOrder, x.IsTerminal, x.TrackName, x.Color
                }),
                parallelTracks = s.ParallelTracks.Where(x => !x.IsDeleted).OrderBy(x => x.SortOrder).Select(x => new
                {
                    x.Id, x.TrackName, x.CompletionStatus, x.SortOrder
                })
            }),
            transitionRules = def.TransitionRules.Where(r => !r.IsDeleted && r.IsActive).OrderBy(r => r.SortOrder).Select(r => new
            {
                r.Id,
                r.SourceStageId,
                r.TargetStageId,
                r.ButtonLabel,
                r.ButtonIcon,
                r.SortOrder,
                conditions = r.Conditions.RootElement,
                r.RequiredFields,
                r.AllowedRoles,
                r.RemoveFromSource,
                r.IsActive
            })
        });
    }
}
