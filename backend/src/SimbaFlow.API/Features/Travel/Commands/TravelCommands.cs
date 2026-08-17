using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Features.Travel.Commands;

public record BookTicketCommand(
    Guid CandidateId,
    string Destination,
    DateOnly FlightDate,
    string? TicketRef = null,
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "travel.update";
}

public class BookTicketHandler : IRequestHandler<BookTicketCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public BookTicketHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(BookTicketCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var gate = await EnsureOnStageAsync(request.CandidateId, TravelArrivalHelpers.TicketStageName, ct);
        if (!gate.IsSuccess)
            return Result.Failure(gate.Error ?? "Failed", gate.StatusCode);

        var meta = new Dictionary<string, string>
        {
            ["destination"] = request.Destination.Trim(),
            ["flight_date"] = request.FlightDate.ToString("yyyy-MM-dd")
        };
        if (!string.IsNullOrWhiteSpace(request.TicketRef))
            meta["ticket_ref"] = request.TicketRef.Trim();

        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, "ticket_status", "Booking Complete", userId,
            _currentUser.UserName ?? "unknown", request.Notes, meta, ct: ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }

    private async Task<Result<Domain.Entities.Candidates.Candidate>> EnsureOnStageAsync(
        Guid candidateId, string stageName, CancellationToken ct)
    {
        var stage = await TravelArrivalHelpers.FindStageByNameAsync(_context, stageName, ct);
        if (stage is null)
            return Result<Domain.Entities.Candidates.Candidate>.Failure($"{stageName} stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result<Domain.Entities.Candidates.Candidate>.Failure("Candidate not found", 404);

        if (!TravelArrivalHelpers.IsVisibleInStage(candidate, stage.Id))
            return Result<Domain.Entities.Candidates.Candidate>.Failure(
                $"Candidate is not on the {stageName} board", 400);

        return Result<Domain.Entities.Candidates.Candidate>.Success(candidate);
    }
}

public record MarkNotifiedCommand(Guid CandidateId, string? Notes = null)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "travel.update";
}

public class MarkNotifiedHandler : IRequestHandler<MarkNotifiedCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;
    private readonly ICandidateNotifier _notifier;

    public MarkNotifiedHandler(
        ITenantDbContext context,
        IWorkflowEngineService engine,
        ICurrentUserService currentUser,
        ICandidateNotifier notifier)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<Result> Handle(MarkNotifiedCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var departure = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.DepartureStageName, ct);
        if (departure is null)
            return Result.Failure("Departure stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!TravelArrivalHelpers.IsVisibleInStage(candidate, departure.Id))
            return Result.Failure("Candidate is not on the Departure board", 400);

        var status = TravelArrivalHelpers.ReadStatusValues(candidate);
        if (TravelArrivalHelpers.IsCanceled(status))
            return Result.Failure("Cannot notify a canceled departure", 400);

        var notification = TravelArrivalHelpers.TrackValue(status, "notification_status");
        if (string.Equals(notification, "Notified", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Candidate is already Notified", 400);

        var meta = new Dictionary<string, string>
        {
            ["notified_at"] = DateTime.UtcNow.ToString("O")
        };

        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, "notification_status", "Notified", userId,
            _currentUser.UserName ?? "unknown", request.Notes, meta, ct: ct);

        if (!result.IsSuccess)
            return Result.Failure(result.Error ?? "Failed", 400);

        await _notifier.NotifyAsync(request.CandidateId, "departure.notified", ct);
        return Result.Success();
    }
}

public record ConfirmDepartedCommand(Guid CandidateId, string? Notes = null)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "travel.update";
}

public class ConfirmDepartedHandler : IRequestHandler<ConfirmDepartedCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public ConfirmDepartedHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ConfirmDepartedCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var departure = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.DepartureStageName, ct);
        var arrival = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.ArrivalStageName, ct);
        if (departure is null || arrival is null)
            return Result.Failure("Departure/Arrival stages not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!TravelArrivalHelpers.IsVisibleInStage(candidate, departure.Id))
            return Result.Failure("Candidate is not on the Departure board", 400);

        var status = TravelArrivalHelpers.ReadStatusValues(candidate);
        if (TravelArrivalHelpers.IsCanceled(status))
            return Result.Failure("Cannot depart a canceled departure", 400);

        var notification = TravelArrivalHelpers.TrackValue(status, "notification_status");
        if (!string.Equals(notification, "Notified", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Candidate must be Notified before Confirm Departed", 400);

        var departureStatus = TravelArrivalHelpers.TrackValue(status, "departure_status");
        if (string.Equals(departureStatus, "Departed", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Candidate is already Departed", 400);

        var transition = await TravelArrivalHelpers.FindTransitionAsync(
            _context, departure.Id, arrival.Id, TravelArrivalHelpers.ToArrivalAction, ct);
        if (transition is null)
            return Result.Failure("To Arrival transition not configured", 500);

        return await RunInTransactionAsync(async () =>
        {
            var meta = new Dictionary<string, string>
            {
                ["departed_at"] = DateTime.UtcNow.ToString("O")
            };

            var statusResult = await _engine.UpdateStatusAsync(
                request.CandidateId, "departure_status", "Departed", userId,
                _currentUser.UserName ?? "unknown", request.Notes, meta,
                saveChanges: false, ct: ct);
            if (!statusResult.IsSuccess)
                return Result.Failure(statusResult.Error ?? "Failed", 400);

            var transitionResult = await _engine.ExecuteTransitionAsync(
                request.CandidateId, transition.Id, userId,
                _currentUser.UserName ?? "unknown", request.Notes, ct);
            if (!transitionResult.IsSuccess)
                return Result.Failure(transitionResult.Error ?? "Transition failed", 400);

            var init = await _engine.UpdateStatusAsync(
                request.CandidateId, TravelArrivalHelpers.ArrivalTrack, "Pending", userId,
                _currentUser.UserName ?? "unknown", null, ct: ct);
            return init.IsSuccess ? Result.Success() : Result.Failure(init.Error ?? "Failed", 400);
        }, ct);
    }

    private async Task<Result> RunInTransactionAsync(Func<Task<Result>> action, CancellationToken ct)
    {
        if (_context is not TenantDbContext db)
            return await action();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action();
            if (result.IsSuccess)
                await tx.CommitAsync(ct);
            else
                await tx.RollbackAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}

public record RecordNotDepartedCommand(
    Guid CandidateId,
    string Reason,
    string Outcome, // BackToTicket | CancelDeparture
    string? ReasonOther = null,
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "travel.update";
}

public class RecordNotDepartedHandler : IRequestHandler<RecordNotDepartedCommand, Result>
{
    private static readonly HashSet<string> AllowedReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(NonDepartureReason.MissedFlight),
        nameof(NonDepartureReason.Immigration),
        nameof(NonDepartureReason.Medical),
        nameof(NonDepartureReason.CandidateNoShow),
        nameof(NonDepartureReason.AirlineCancel),
        nameof(NonDepartureReason.Other)
    };

    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public RecordNotDepartedHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RecordNotDepartedCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var reason = TravelArrivalHelpers.NormalizeReason(request.Reason);
        if (!AllowedReasons.Contains(reason))
            return Result.Failure("Invalid non-departure reason", 400);
        if (reason.Equals("Other", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(request.ReasonOther))
            return Result.Failure("ReasonOther is required when Reason is Other", 400);

        var outcome = request.Outcome.Trim();
        var backToTicket = outcome.Equals("BackToTicket", StringComparison.OrdinalIgnoreCase);
        var cancel = outcome.Equals("CancelDeparture", StringComparison.OrdinalIgnoreCase);
        if (!backToTicket && !cancel)
            return Result.Failure("Outcome must be BackToTicket or CancelDeparture", 400);

        var departure = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.DepartureStageName, ct);
        var ticket = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.TicketStageName, ct);
        if (departure is null || ticket is null)
            return Result.Failure("Departure/Ticket stages not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!TravelArrivalHelpers.IsVisibleInStage(candidate, departure.Id))
            return Result.Failure("Candidate is not on the Departure board", 400);

        var status = TravelArrivalHelpers.ReadStatusValues(candidate);
        if (TravelArrivalHelpers.IsCanceled(status))
            return Result.Failure("Departure is already canceled", 400);
        if (string.Equals(
                TravelArrivalHelpers.TrackValue(status, "departure_status"),
                "Departed",
                StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Cannot mark Not Departed after Departed", 400);

        return await RunInTransactionAsync(async () =>
        {
            var meta = new Dictionary<string, string>
            {
                ["non_departure_reason"] = reason,
                ["departure_outcome"] = backToTicket ? "Rebooked" : "Canceled",
                ["canceled"] = cancel ? "true" : "false"
            };
            if (!string.IsNullOrWhiteSpace(request.ReasonOther))
                meta["non_departure_reason_other"] = request.ReasonOther.Trim();

            var statusResult = await _engine.UpdateStatusAsync(
                request.CandidateId, "departure_status", "Not Departed", userId,
                _currentUser.UserName ?? "unknown", request.Notes, meta,
                saveChanges: !backToTicket, ct: ct);
            if (!statusResult.IsSuccess)
                return Result.Failure(statusResult.Error ?? "Failed", 400);

            if (cancel)
                return Result.Success();

            var transition = await TravelArrivalHelpers.FindTransitionAsync(
                _context, departure.Id, ticket.Id, TravelArrivalHelpers.BackToTicketAction, ct);
            if (transition is null)
                return Result.Failure("Back to Ticket transition not configured", 500);

            var transitionResult = await _engine.ExecuteTransitionAsync(
                request.CandidateId, transition.Id, userId,
                _currentUser.UserName ?? "unknown", request.Notes, ct);
            if (!transitionResult.IsSuccess)
                return Result.Failure(transitionResult.Error ?? "Transition failed", 400);

            var reset = await _engine.UpdateStatusAsync(
                request.CandidateId, "ticket_status", "Pending", userId,
                _currentUser.UserName ?? "unknown", "Reset for rebook after Not Departed", ct: ct);
            return reset.IsSuccess ? Result.Success() : Result.Failure(reset.Error ?? "Failed", 400);
        }, ct);
    }

    private async Task<Result> RunInTransactionAsync(Func<Task<Result>> action, CancellationToken ct)
    {
        if (_context is not TenantDbContext db)
            return await action();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action();
            if (result.IsSuccess)
                await tx.CommitAsync(ct);
            else
                await tx.RollbackAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
