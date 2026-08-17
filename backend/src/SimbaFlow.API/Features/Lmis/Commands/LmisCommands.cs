using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Embassy;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Features.Lmis.Commands;

public record RecordInsurancePaidCommand(
    Guid CandidateId,
    DateOnly? PaymentDate = null,
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "lmis.update";
}

public class RecordInsurancePaidHandler : IRequestHandler<RecordInsurancePaidCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public RecordInsurancePaidHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RecordInsurancePaidCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var lmis = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.LmisStageName, ct);
        if (lmis is null)
            return Result.Failure("LMIS stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!EmbassyLmisHelpers.IsVisibleInStage(candidate, lmis.Id))
            return Result.Failure("Candidate is not visible on the LMIS board", 400);

        var insurance = EmbassyLmisHelpers.TrackValue(
            EmbassyLmisHelpers.ReadStatusValues(candidate), "insurance");
        if (string.Equals(insurance, "Available", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(insurance, "Insurance Paid", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure($"Insurance already recorded as '{insurance}'", 400);
        }

        var paymentDate = (request.PaymentDate ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToString("yyyy-MM-dd");

        var result = await _engine.UpdateStatusChainAsync(
            request.CandidateId,
            [
                new StatusChange(
                    "insurance",
                    "Insurance Paid",
                    request.Notes,
                    new Dictionary<string, string> { ["insurance_payment_date"] = paymentDate }),
                new StatusChange("insurance", "Available")
            ],
            userId,
            _currentUser.UserName ?? "unknown",
            ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }
}

public record AdvanceLmisMilestoneCommand(
    Guid CandidateId,
    string Milestone,
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "lmis.update";
}

public class AdvanceLmisMilestoneHandler : IRequestHandler<AdvanceLmisMilestoneCommand, Result>
{
    private static readonly Dictionary<string, string> NextAllowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [""] = "Uploaded",
        ["Uploaded"] = "Check Verified",
        ["Check Verified"] = "Issued"
    };

    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public AdvanceLmisMilestoneHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(AdvanceLmisMilestoneCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var lmis = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.LmisStageName, ct);
        if (lmis is null)
            return Result.Failure("LMIS stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!EmbassyLmisHelpers.IsVisibleInStage(candidate, lmis.Id))
            return Result.Failure("Candidate is not visible on the LMIS board", 400);

        var status = EmbassyLmisHelpers.ReadStatusValues(candidate);
        var insurance = EmbassyLmisHelpers.TrackValue(status, "insurance");
        if (!string.Equals(insurance, "Available", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Insurance must be Available before advancing milestones", 400);

        var current = EmbassyLmisHelpers.TrackValue(status, "milestone") ?? "";
        if (string.Equals(current, "Issued", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Milestone is already Issued", 400);

        if (!NextAllowed.TryGetValue(current, out var expectedNext))
            return Result.Failure($"Unknown current milestone '{current}'", 400);

        var requested = request.Milestone.Trim();
        if (!requested.Equals(expectedNext, StringComparison.OrdinalIgnoreCase))
            return Result.Failure($"Next milestone must be '{expectedNext}' (requested '{requested}')", 400);

        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, "milestone", expectedNext, userId,
            _currentUser.UserName ?? "unknown", request.Notes, ct: ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }
}
