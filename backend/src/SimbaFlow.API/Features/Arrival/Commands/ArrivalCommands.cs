using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Travel;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Entities.Travel;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Features.Arrival.Commands;

public record ConfirmArrivedCommand(Guid CandidateId, string? Notes = null)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "arrival.update";
}

public class ConfirmArrivedHandler : IRequestHandler<ConfirmArrivedCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public ConfirmArrivedHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ConfirmArrivedCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var gate = await EnsureArrivalAsync(request.CandidateId, ct);
        if (!gate.IsSuccess)
            return Result.Failure(gate.Error ?? "Failed", gate.StatusCode);

        var arrival = TravelArrivalHelpers.TrackValue(
            TravelArrivalHelpers.ReadStatusValues(gate.Data!), TravelArrivalHelpers.ArrivalTrack);
        if (!string.IsNullOrEmpty(arrival) &&
            !arrival.Equals("Pending", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure($"Cannot confirm Arrived from status '{arrival}'. Expected Pending.", 400);
        }

        var meta = new Dictionary<string, string>
        {
            ["arrived_at"] = DateTime.UtcNow.ToString("O")
        };

        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, TravelArrivalHelpers.ArrivalTrack, "Arrived", userId,
            _currentUser.UserName ?? "unknown", request.Notes, meta, ct: ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }

    private async Task<Result<Domain.Entities.Candidates.Candidate>> EnsureArrivalAsync(
        Guid candidateId, CancellationToken ct)
    {
        var stage = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.ArrivalStageName, ct);
        if (stage is null)
            return Result<Domain.Entities.Candidates.Candidate>.Failure("Arrival stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result<Domain.Entities.Candidates.Candidate>.Failure("Candidate not found", 404);

        if (!TravelArrivalHelpers.IsVisibleInStage(candidate, stage.Id))
            return Result<Domain.Entities.Candidates.Candidate>.Failure(
                "Candidate is not on the Arrival board", 400);

        return Result<Domain.Entities.Candidates.Candidate>.Success(candidate);
    }
}

public record FlagExceptionCommand(
    Guid CandidateId,
    string Type, // Returned | Runaway
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "arrival.update";
}

public class FlagExceptionHandler : IRequestHandler<FlagExceptionCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public FlagExceptionHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(FlagExceptionCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var typeRaw = request.Type.Trim();
        ExceptionType type;
        string statusValue;
        if (typeRaw.Equals("Returned", StringComparison.OrdinalIgnoreCase))
        {
            type = ExceptionType.Returned;
            statusValue = "Returned";
        }
        else if (typeRaw.Equals("Runaway", StringComparison.OrdinalIgnoreCase))
        {
            type = ExceptionType.Runaway;
            statusValue = "Runaway";
        }
        else
        {
            return Result.Failure("Type must be Returned or Runaway", 400);
        }

        var stage = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.ArrivalStageName, ct);
        if (stage is null)
            return Result.Failure("Arrival stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!TravelArrivalHelpers.IsVisibleInStage(candidate, stage.Id))
            return Result.Failure("Candidate is not on the Arrival board", 400);

        var openExists = await _context.ExceptionCases.AnyAsync(e =>
            e.CandidateId == request.CandidateId
            && !e.IsDeleted
            && e.Status == ExceptionStatus.Open, ct);
        if (openExists)
            return Result.Failure("An open exception case already exists for this candidate", 409);

        if (_context is not TenantDbContext db)
            return Result.Failure("Database transaction unavailable", 500);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var statusResult = await _engine.UpdateStatusAsync(
                request.CandidateId, TravelArrivalHelpers.ArrivalTrack, statusValue, userId,
                _currentUser.UserName ?? "unknown", request.Notes, ct: ct);
            if (!statusResult.IsSuccess)
            {
                await tx.RollbackAsync(ct);
                return Result.Failure(statusResult.Error ?? "Failed", 400);
            }

            _context.ExceptionCases.Add(new ExceptionCase
            {
                CandidateId = request.CandidateId,
                Type = type,
                Status = ExceptionStatus.Open,
                OpenedAt = DateTime.UtcNow,
                OpenedByUserId = userId
            });
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}

public record AddToCommissionCommand(Guid CandidateId, string? Notes = null)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "arrival.update";
}

public class AddToCommissionHandler : IRequestHandler<AddToCommissionCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public AddToCommissionHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(AddToCommissionCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var arrival = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.ArrivalStageName, ct);
        var commissionStage = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.CommissionStageName, ct);
        if (arrival is null || commissionStage is null)
            return Result.Failure("Arrival/Commission stages not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!TravelArrivalHelpers.IsVisibleInStage(candidate, arrival.Id))
            return Result.Failure("Candidate is not on the Arrival board", 400);

        var status = TravelArrivalHelpers.ReadStatusValues(candidate);
        var arrivalStatus = TravelArrivalHelpers.TrackValue(status, TravelArrivalHelpers.ArrivalTrack);
        if (!string.Equals(arrivalStatus, "Arrived", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Add to Commission requires Arrived status", 400);

        var openException = await _context.ExceptionCases.AnyAsync(e =>
            e.CandidateId == request.CandidateId
            && !e.IsDeleted
            && e.Status == ExceptionStatus.Open, ct);
        if (openException)
            return Result.Failure("Cannot add to Commission while an open exception exists", 400);

        var transition = await TravelArrivalHelpers.FindTransitionAsync(
            _context, arrival.Id, commissionStage.Id, TravelArrivalHelpers.AddToCommissionAction, ct);
        if (transition is null)
            return Result.Failure("Add to Commission transition not configured", 500);

        if (_context is not TenantDbContext db)
            return Result.Failure("Database transaction unavailable", 500);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Stamp the arrival row as commission-linked BEFORE moving stages.
            // Status values are validated against the candidate's CURRENT stage, and
            // "Arrived" belongs to Arrival — once the transition runs the current stage
            // is Commission (Settled/Disputed/...), which would reject this write and
            // roll back the whole operation.
            var link = await _engine.UpdateStatusAsync(
                request.CandidateId, TravelArrivalHelpers.ArrivalTrack, "Arrived", userId,
                _currentUser.UserName ?? "unknown",
                request.Notes,
                new Dictionary<string, string> { ["commission_linked"] = "true" },
                ct: ct);
            if (!link.IsSuccess)
            {
                await tx.RollbackAsync(ct);
                return Result.Failure(link.Error ?? "Failed to mark commission link", 400);
            }

            var transitionResult = await _engine.ExecuteTransitionAsync(
                request.CandidateId, transition.Id, userId,
                _currentUser.UserName ?? "unknown", request.Notes, ct);
            if (!transitionResult.IsSuccess)
            {
                await tx.RollbackAsync(ct);
                return Result.Failure(transitionResult.Error ?? "Transition failed", 400);
            }

            var shell = await _context.Commissions
                .FirstOrDefaultAsync(c => c.CandidateId == request.CandidateId && !c.IsDeleted, ct);
            if (shell is null)
            {
                _context.Commissions.Add(new Commission
                {
                    CandidateId = request.CandidateId,
                    Status = CommissionStatus.Open,
                    CountryOfTravel = TravelArrivalHelpers.TrackValue(status, "destination")
                                      ?? candidate.CountryOfTravel,
                    PartnerName = candidate.PartnerName,
                    ContractDate = null,
                    OpenedAt = DateTime.UtcNow,
                    OpenedByUserId = userId
                });
            }

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
