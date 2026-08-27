using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Features.Embassy.Commands;

public record BookMedicalCommand(
    Guid CandidateId,
    // Appointment date and facility are optional — agencies just mark medical booked / not booked.
    // Kept nullable rather than removed so any older caller still compiles.
    DateOnly? AppointmentDate = null,
    string? FacilityName = null,
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "embassy.update";
}

public class BookMedicalHandler : IRequestHandler<BookMedicalCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public BookMedicalHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(BookMedicalCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var gate = await EnsureEmbassyAsync(request.CandidateId, ct);
        if (!gate.IsSuccess)
            return Result.Failure(gate.Error ?? "Failed", gate.StatusCode);

        var status = EmbassyLmisHelpers.ReadStatusValues(gate.Data!);
        var medical = EmbassyLmisHelpers.TrackValue(status, "medical");
        if (!string.IsNullOrEmpty(medical) &&
            !medical.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
            !medical.Equals("Unfit", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure($"Cannot book medical from status '{medical}'. Expected Pending or Unfit.", 400);
        }

        var meta = new Dictionary<string, string>();
        if (request.AppointmentDate is DateOnly d)
            meta["medical_appointment_date"] = d.ToString("yyyy-MM-dd");
        if (!string.IsNullOrWhiteSpace(request.FacilityName))
            meta["medical_facility"] = request.FacilityName;

        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, "medical", "Booked", userId,
            _currentUser.UserName ?? "unknown", request.Notes, meta, ct: ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }

    private async Task<Result<Domain.Entities.Candidates.Candidate>> EnsureEmbassyAsync(
        Guid candidateId, CancellationToken ct)
    {
        var embassy = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.EmbassyStageName, ct);
        if (embassy is null)
            return Result<Domain.Entities.Candidates.Candidate>.Failure("Embassy stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result<Domain.Entities.Candidates.Candidate>.Failure("Candidate not found", 404);

        if (!EmbassyLmisHelpers.IsVisibleInStage(candidate, embassy.Id))
            return Result<Domain.Entities.Candidates.Candidate>.Failure(
                "Candidate is not in the Embassy stage", 400);

        return Result<Domain.Entities.Candidates.Candidate>.Success(candidate);
    }
}

public record RecordMedicalResultCommand(
    Guid CandidateId,
    string Result, // Fit | Unfit
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "embassy.update";
}

public class RecordMedicalResultHandler : IRequestHandler<RecordMedicalResultCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public RecordMedicalResultHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RecordMedicalResultCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var outcome = request.Result.Trim();
        if (!outcome.Equals("Fit", StringComparison.OrdinalIgnoreCase) &&
            !outcome.Equals("Unfit", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Result must be Fit or Unfit", 400);

        var embassy = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.EmbassyStageName, ct);
        if (embassy is null)
            return Result.Failure("Embassy stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!EmbassyLmisHelpers.IsVisibleInStage(candidate, embassy.Id))
            return Result.Failure("Candidate is not in the Embassy stage", 400);

        var medical = EmbassyLmisHelpers.TrackValue(
            EmbassyLmisHelpers.ReadStatusValues(candidate), "medical");
        if (!string.Equals(medical, "Booked", StringComparison.OrdinalIgnoreCase))
            return Result.Failure($"Medical must be Booked before recording result (current: {medical ?? "empty"})", 400);

        var normalized = outcome.Equals("Fit", StringComparison.OrdinalIgnoreCase) ? "Fit" : "Unfit";
        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, "medical", normalized, userId,
            _currentUser.UserName ?? "unknown", request.Notes, ct: ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }
}

public record BookTasheerCommand(
    Guid CandidateId,
    DateOnly AppointmentDate,
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "embassy.update";
}

public class BookTasheerHandler : IRequestHandler<BookTasheerCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public BookTasheerHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(BookTasheerCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var embassy = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.EmbassyStageName, ct);
        if (embassy is null)
            return Result.Failure("Embassy stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!EmbassyLmisHelpers.IsVisibleInStage(candidate, embassy.Id))
            return Result.Failure("Candidate is not in the Embassy stage", 400);

        var tasheer = EmbassyLmisHelpers.TrackValue(
            EmbassyLmisHelpers.ReadStatusValues(candidate), "tasheer");
        if (!string.IsNullOrEmpty(tasheer) &&
            !tasheer.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
            !tasheer.Equals("Expired", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure($"Cannot book Tasheer from status '{tasheer}'. Expected Pending or Expired.", 400);
        }

        var meta = new Dictionary<string, string>
        {
            ["tasheer_appointment_date"] = request.AppointmentDate.ToString("yyyy-MM-dd")
        };

        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, "tasheer", "Booked", userId,
            _currentUser.UserName ?? "unknown", request.Notes, meta, ct: ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }
}

public record RecordTasheerResultCommand(
    Guid CandidateId,
    string Result, // Book Done | Expired
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "embassy.update";
}

public class RecordTasheerResultHandler : IRequestHandler<RecordTasheerResultCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public RecordTasheerResultHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RecordTasheerResultCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var outcome = request.Result.Trim();
        var isBookDone = outcome.Equals("Book Done", StringComparison.OrdinalIgnoreCase);
        var isExpired = outcome.Equals("Expired", StringComparison.OrdinalIgnoreCase);
        if (!isBookDone && !isExpired)
            return Result.Failure("Result must be 'Book Done' or 'Expired'", 400);

        var embassy = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.EmbassyStageName, ct);
        if (embassy is null)
            return Result.Failure("Embassy stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!EmbassyLmisHelpers.IsVisibleInStage(candidate, embassy.Id))
            return Result.Failure("Candidate is not in the Embassy stage", 400);

        var tasheer = EmbassyLmisHelpers.TrackValue(
            EmbassyLmisHelpers.ReadStatusValues(candidate), "tasheer");
        if (!string.Equals(tasheer, "Booked", StringComparison.OrdinalIgnoreCase))
            return Result.Failure($"Tasheer must be Booked before recording result (current: {tasheer ?? "empty"})", 400);

        var normalized = isBookDone ? "Book Done" : "Expired";
        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, "tasheer", normalized, userId,
            _currentUser.UserName ?? "unknown", request.Notes, ct: ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }
}

public record SetVisaReadyCommand(Guid CandidateId, string? Notes = null)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "embassy.update";
}

public class SetVisaReadyHandler : IRequestHandler<SetVisaReadyCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public SetVisaReadyHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(SetVisaReadyCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var embassy = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.EmbassyStageName, ct);
        if (embassy is null)
            return Result.Failure("Embassy stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!EmbassyLmisHelpers.IsVisibleInStage(candidate, embassy.Id))
            return Result.Failure("Candidate is not in the Embassy stage", 400);

        var status = EmbassyLmisHelpers.ReadStatusValues(candidate);
        var medical = EmbassyLmisHelpers.TrackValue(status, "medical");
        var tasheer = EmbassyLmisHelpers.TrackValue(status, "tasheer");
        if (!string.Equals(medical, "Fit", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(tasheer, "Book Done", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("Clearances incomplete: medical must be Fit and tasheer must be Book Done", 400);
        }

        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, "visa", "Ready", userId,
            _currentUser.UserName ?? "unknown", request.Notes, ct: ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }
}

public record SubmitVisaDocumentationCommand(
    Guid CandidateId,
    DateOnly? SubmissionDate = null,
    string? ReferenceNumber = null,
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "embassy.case_submit";
}

public class SubmitVisaDocumentationHandler : IRequestHandler<SubmitVisaDocumentationCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public SubmitVisaDocumentationHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(SubmitVisaDocumentationCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var caseExec = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.CaseExecutiveStageName, ct);
        var embassy = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.EmbassyStageName, ct);
        if (caseExec is null || embassy is null)
            return Result.Failure("Case Executive / Embassy stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);

        if (!EmbassyLmisHelpers.IsVisibleInStage(candidate, caseExec.Id) &&
            !EmbassyLmisHelpers.IsVisibleInStage(candidate, embassy.Id))
            return Result.Failure("Candidate is not on the Case Executive board", 400);

        var visa = EmbassyLmisHelpers.TrackValue(
            EmbassyLmisHelpers.ReadStatusValues(candidate), "visa");
        if (!string.Equals(visa, "Ready", StringComparison.OrdinalIgnoreCase))
            return Result.Failure($"Visa must be Ready before submit (current: {visa ?? "empty"})", 400);

        var meta = new Dictionary<string, string>();
        if (request.SubmissionDate.HasValue)
            meta["visa_submission_date"] = request.SubmissionDate.Value.ToString("yyyy-MM-dd");
        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber))
            meta["visa_reference_number"] = request.ReferenceNumber.Trim();

        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, "visa", "Submitted", userId,
            _currentUser.UserName ?? "unknown", request.Notes,
            meta.Count > 0 ? meta : null, ct: ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }
}

public record RecordVisaOutcomeCommand(
    Guid CandidateId,
    string Outcome, // Issued | Rejected
    string? RejectionReason = null,
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "embassy.visa_outcome";
}

public class RecordVisaOutcomeHandler : IRequestHandler<RecordVisaOutcomeCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public RecordVisaOutcomeHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RecordVisaOutcomeCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var isIssued = request.Outcome.Equals("Issued", StringComparison.OrdinalIgnoreCase);
        var isRejected = request.Outcome.Equals("Rejected", StringComparison.OrdinalIgnoreCase);
        if (!isIssued && !isRejected)
            return Result.Failure("Outcome must be Issued or Rejected", 400);

        if (isRejected && string.IsNullOrWhiteSpace(request.RejectionReason))
            return Result.Failure("Rejection reason is required", 400);

        var embassy = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.EmbassyStageName, ct);
        if (embassy is null)
            return Result.Failure("Embassy stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!EmbassyLmisHelpers.IsVisibleInStage(candidate, embassy.Id))
            return Result.Failure("Candidate is not in the Embassy stage", 400);

        var status = EmbassyLmisHelpers.ReadStatusValues(candidate);
        var visa = EmbassyLmisHelpers.TrackValue(status, "visa");
        if (!string.Equals(visa, "Submitted", StringComparison.OrdinalIgnoreCase))
            return Result.Failure($"Visa must be Submitted before outcome (current: {visa ?? "empty"})", 400);

        Dictionary<string, string>? meta = null;
        if (isRejected)
        {
            meta = new Dictionary<string, string>
            {
                ["visa_rejection_reason"] = request.RejectionReason!.Trim()
            };
        }

        var normalized = isIssued ? "Issued" : "Rejected";
        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, "visa", normalized, userId,
            _currentUser.UserName ?? "unknown", request.Notes, meta, ct: ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }
}

public record ResubmitVisaCommand(Guid CandidateId, string? Notes = null)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "embassy.visa_outcome";
}

public class ResubmitVisaHandler : IRequestHandler<ResubmitVisaCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public ResubmitVisaHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ResubmitVisaCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var embassy = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.EmbassyStageName, ct);
        if (embassy is null)
            return Result.Failure("Embassy stage not configured", 500);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result.Failure("Candidate not found", 404);
        if (!EmbassyLmisHelpers.IsVisibleInStage(candidate, embassy.Id))
            return Result.Failure("Candidate is not in the Embassy stage", 400);

        var status = EmbassyLmisHelpers.ReadStatusValues(candidate);
        var visa = EmbassyLmisHelpers.TrackValue(status, "visa");
        if (!string.Equals(visa, "Rejected", StringComparison.OrdinalIgnoreCase))
            return Result.Failure($"Resubmit only allowed from Rejected (current: {visa ?? "empty"})", 400);

        var attempt = 1;
        if (status.TryGetValue("visa_resubmission_count", out var raw) &&
            int.TryParse(raw, out var existing))
            attempt = existing + 1;

        var meta = new Dictionary<string, string>
        {
            ["visa_resubmission_count"] = attempt.ToString(),
            ["action"] = "Resubmit"
        };
        if (status.TryGetValue("visa_rejection_reason", out var reason) && !string.IsNullOrEmpty(reason))
            meta["preserved_rejection_reason"] = reason;

        var result = await _engine.UpdateStatusAsync(
            request.CandidateId, "visa", "Ready", userId,
            _currentUser.UserName ?? "unknown", request.Notes, meta, ct: ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error ?? "Failed", 400);
    }
}
