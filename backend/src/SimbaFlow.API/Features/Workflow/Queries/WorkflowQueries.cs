using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Features.Workflow.Queries;

public record AvailableActionDto(
    Guid TransitionRuleId,
    Guid SourceStageId,
    string ButtonLabel,
    string? ButtonIcon,
    bool IsEnabled,
    string? DisabledReason);

public record GetAvailableActionsQuery(Guid CandidateId, Guid? SourceStageId = null)
    : IRequest<Result<List<AvailableActionDto>>>, IRequirePermission
{
    public string RequiredPermission => "workflow.view";
}

public class GetAvailableActionsHandler : IRequestHandler<GetAvailableActionsQuery, Result<List<AvailableActionDto>>>
{
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public GetAvailableActionsHandler(IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result<List<AvailableActionDto>>> Handle(
        GetAvailableActionsQuery request, CancellationToken cancellationToken)
    {
        var actions = await _engine.GetAvailableActionsAsync(
            request.CandidateId,
            _currentUser.Roles.ToArray(),
            cancellationToken);

        // A candidate is visible on several boards at once through mirror stages, and the engine
        // returns the transitions of every one of them. Unfiltered, the LMIS board offered
        // "To LMIS" and the Departure/Arrival boards offered "To Ticket" — each board must only
        // offer the steps that leave its own stage.
        if (request.SourceStageId is Guid stageId)
            actions = actions.Where(a => a.SourceStageId == stageId).ToList();

        var dtos = actions.Select(a => new AvailableActionDto(
            a.TransitionRuleId, a.SourceStageId, a.ButtonLabel, a.ButtonIcon, a.IsEnabled, a.DisabledReason)).ToList();

        return Result<List<AvailableActionDto>>.Success(dtos);
    }
}

public record WorkflowStateDto(
    Guid? StageId,
    string? StageName,
    Dictionary<string, string> StatusValues,
    Guid[] VisibleInStages);

public record GetWorkflowStateQuery(Guid CandidateId)
    : IRequest<Result<WorkflowStateDto>>, IRequirePermission
{
    public string RequiredPermission => "workflow.view";
}

public class GetWorkflowStateHandler : IRequestHandler<GetWorkflowStateQuery, Result<WorkflowStateDto>>
{
    private readonly IWorkflowEngineService _engine;

    public GetWorkflowStateHandler(IWorkflowEngineService engine) => _engine = engine;

    public async Task<Result<WorkflowStateDto>> Handle(
        GetWorkflowStateQuery request, CancellationToken cancellationToken)
    {
        var state = await _engine.GetCurrentStateAsync(request.CandidateId, cancellationToken);
        return Result<WorkflowStateDto>.Success(new WorkflowStateDto(
            state.StageId,
            state.StageName,
            state.StatusValues,
            state.VisibleInStages.ToArray()));
    }
}

public record WorkflowEventDto(
    Guid Id,
    long SequenceNumber,
    int EventType,
    Guid? FromStageId,
    string? FromStageName,
    Guid? ToStageId,
    string? ToStageName,
    string UserName,
    DateTime Timestamp,
    string? Notes);

public record GetWorkflowEventsQuery(Guid CandidateId)
    : IRequest<Result<List<WorkflowEventDto>>>, IRequirePermission
{
    public string RequiredPermission => "workflow.view";
}

public class GetWorkflowEventsHandler : IRequestHandler<GetWorkflowEventsQuery, Result<List<WorkflowEventDto>>>
{
    private readonly ITenantDbContext _context;

    public GetWorkflowEventsHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<List<WorkflowEventDto>>> Handle(
        GetWorkflowEventsQuery request, CancellationToken cancellationToken)
    {
        var events = await _context.WorkflowEvents
            .AsNoTracking()
            .Where(e => e.CandidateId == request.CandidateId)
            .OrderByDescending(e => e.SequenceNumber)
            .Select(e => new WorkflowEventDto(
                e.Id, e.SequenceNumber, (int)e.EventType,
                e.FromStageId, e.FromStageName, e.ToStageId, e.ToStageName,
                e.UserName, e.Timestamp, e.Notes))
            .ToListAsync(cancellationToken);

        return Result<List<WorkflowEventDto>>.Success(events);
    }
}

public record ViewCandidateDto(
    Guid Id,
    string FullName,
    string PassportNumber,
    string? LabourId,
    string? CurrentStageName,
    Guid? CurrentStageId,
    Dictionary<string, string> StatusValues,
    string? CountryOfTravel,
    string? PartnerName,
    DateTime RegisteredAt,
    bool IsMirror);

public record PaginatedViewResult(
    List<ViewCandidateDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record GetViewCandidatesQuery(
    Guid StageId, int Page, int PageSize, string? Search)
    : IRequest<Result<PaginatedViewResult>>, IRequirePermission
{
    public string RequiredPermission => "workflow.view";
}

public class GetViewCandidatesHandler : IRequestHandler<GetViewCandidatesQuery, Result<PaginatedViewResult>>
{
    private readonly ITenantDbContext _context;

    public GetViewCandidatesHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<PaginatedViewResult>> Handle(
        GetViewCandidatesQuery request, CancellationToken cancellationToken)
    {
        // VisibleInStages is JSON text via value converter — cannot use .Contains() in SQL.
        // Apply status/search/office in SQL, then filter stage membership in memory.
        var query = _context.Candidates
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(c =>
                EF.Functions.ILike(c.FirstName, $"%{search}%") ||
                EF.Functions.ILike(c.LastName, $"%{search}%") ||
                EF.Functions.ILike(c.PassportNumber, $"%{search}%") ||
                (c.LabourId != null && EF.Functions.ILike(c.LabourId, $"%{search}%")));
        }

        var candidates = await query
            .OrderByDescending(c => c.RegisteredAt)
            .ToListAsync(cancellationToken);

        var stageMatches = candidates
            .Where(c =>
                c.CurrentStageId == request.StageId
                || c.VisibleInStages.Contains(request.StageId))
            .ToList();

        var totalCount = stageMatches.Count;
        var rows = stageMatches
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var items = rows.Select(c =>
        {
            var status = new Dictionary<string, string>();
            if (c.CurrentStatusValues is not null &&
                c.CurrentStatusValues.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in c.CurrentStatusValues.RootElement.EnumerateObject())
                    status[prop.Name] = prop.Value.GetString() ?? "";
            }

            var isMirror = c.CurrentStageId != request.StageId
                && c.VisibleInStages.Contains(request.StageId);

            return new ViewCandidateDto(
                c.Id,
                c.FullName,
                c.PassportNumber,
                c.LabourId,
                c.CurrentStageName,
                c.CurrentStageId,
                status,
                c.CountryOfTravel,
                c.PartnerName,
                c.RegisteredAt,
                isMirror);
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
        return Result<PaginatedViewResult>.Success(
            new PaginatedViewResult(items, totalCount, request.Page, request.PageSize, totalPages));
    }
}

public record WorkflowDefinitionDto(
    Guid Id,
    string Name,
    string? Description,
    int Version,
    bool IsActive,
    List<WorkflowStageDto> Stages,
    List<WorkflowTransitionRuleDto> TransitionRules);

public record WorkflowStageDto(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder,
    int StageType,
    bool IsInitialStage,
    bool IsFinalStage,
    List<WorkflowStageStatusDto> Statuses,
    List<ParallelTrackDto> ParallelTracks);

public record WorkflowStageStatusDto(
    Guid Id, string Name, int SortOrder, bool IsTerminal, string? TrackName, string? Color);

public record ParallelTrackDto(Guid Id, string TrackName, string CompletionStatus, int SortOrder);

public record WorkflowTransitionRuleDto(
    Guid Id,
    Guid SourceStageId,
    Guid TargetStageId,
    string ButtonLabel,
    string? ButtonIcon,
    int SortOrder,
    object? Conditions,
    string[] RequiredFields,
    string[] AllowedRoles,
    bool RemoveFromSource,
    bool IsActive);

public record GetWorkflowDefinitionQuery
    : IRequest<Result<WorkflowDefinitionDto>>, IRequirePermission
{
    // Readable by stage-board users (not only admins) so nav slugs can resolve to stage IDs
    public string RequiredPermission => "workflow.view";
}

public class GetWorkflowDefinitionHandler : IRequestHandler<GetWorkflowDefinitionQuery, Result<WorkflowDefinitionDto>>
{
    private readonly ITenantDbContext _context;

    public GetWorkflowDefinitionHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<WorkflowDefinitionDto>> Handle(
        GetWorkflowDefinitionQuery request, CancellationToken cancellationToken)
    {
        var definition = await _context.WorkflowDefinitions
            .AsNoTracking()
            .Include(w => w.Stages.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Statuses.Where(st => !st.IsDeleted))
            .Include(w => w.Stages)
                .ThenInclude(s => s.ParallelTracks.Where(t => !t.IsDeleted))
            .Include(w => w.TransitionRules.Where(r => !r.IsDeleted))
            .FirstOrDefaultAsync(w => w.IsActive && !w.IsDeleted, cancellationToken);

        if (definition is null)
            return Result<WorkflowDefinitionDto>.Failure("No active workflow definition found.", 404);

        var dto = new WorkflowDefinitionDto(
            definition.Id,
            definition.Name,
            definition.Description,
            definition.Version,
            definition.IsActive,
            definition.Stages
                .OrderBy(s => s.SortOrder)
                .Select(s => new WorkflowStageDto(
                    s.Id, s.Name, s.Description, s.SortOrder, (int)s.StageType,
                    s.IsInitialStage, s.IsFinalStage,
                    s.Statuses.OrderBy(st => st.SortOrder)
                        .Select(st => new WorkflowStageStatusDto(
                            st.Id, st.Name, st.SortOrder, st.IsTerminal, st.TrackName, st.Color))
                        .ToList(),
                    s.ParallelTracks.OrderBy(t => t.SortOrder)
                        .Select(t => new ParallelTrackDto(t.Id, t.TrackName, t.CompletionStatus, t.SortOrder))
                        .ToList()))
                .ToList(),
            definition.TransitionRules
                .OrderBy(r => r.SortOrder)
                .Select(r => new WorkflowTransitionRuleDto(
                    r.Id, r.SourceStageId, r.TargetStageId, r.ButtonLabel, r.ButtonIcon, r.SortOrder,
                    r.Conditions.RootElement.Clone(),
                    r.RequiredFields, r.AllowedRoles, r.RemoveFromSource, r.IsActive))
                .ToList());

        return Result<WorkflowDefinitionDto>.Success(dto);
    }
}
