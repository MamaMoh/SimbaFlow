using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Travel;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Exceptions.Commands;

public record AddInvestigationNoteCommand(
    Guid ExceptionCaseId,
    string Body,
    Guid[]? AttachmentDocumentIds = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "arrival.exception";
}

public class AddInvestigationNoteHandler : IRequestHandler<AddInvestigationNoteCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddInvestigationNoteHandler(ITenantDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(AddInvestigationNoteCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var exceptionCase = await _context.ExceptionCases
            .FirstOrDefaultAsync(e => e.Id == request.ExceptionCaseId && !e.IsDeleted, ct);
        if (exceptionCase is null)
            return Result.Failure("Exception case not found", 404);
        if (exceptionCase.Status == ExceptionStatus.Closed)
            return Result.Failure("Cannot add notes to a closed case", 400);

        _context.InvestigationNotes.Add(new InvestigationNote
        {
            ExceptionCaseId = exceptionCase.Id,
            AuthorUserId = userId,
            Body = request.Body.Trim(),
            AttachmentDocumentIds = request.AttachmentDocumentIds ?? []
        });
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record UpdateExceptionStatusCommand(
    Guid ExceptionCaseId,
    string Status) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "arrival.exception";
}

public class UpdateExceptionStatusHandler : IRequestHandler<UpdateExceptionStatusCommand, Result>
{
    private readonly ITenantDbContext _context;

    public UpdateExceptionStatusHandler(ITenantDbContext context) => _context = context;

    public async Task<Result> Handle(UpdateExceptionStatusCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ExceptionStatus>(request.Status, ignoreCase: true, out var status) ||
            status == ExceptionStatus.Closed)
        {
            return Result.Failure(
                "Status must be Open, UnderInvestigation, or Resolved (use close endpoint for Closed)", 400);
        }

        var exceptionCase = await _context.ExceptionCases
            .FirstOrDefaultAsync(e => e.Id == request.ExceptionCaseId && !e.IsDeleted, ct);
        if (exceptionCase is null)
            return Result.Failure("Exception case not found", 404);
        if (exceptionCase.Status == ExceptionStatus.Closed)
            return Result.Failure("Case is already closed", 400);

        exceptionCase.Status = status;
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record AssignLiabilityCommand(
    Guid ExceptionCaseId,
    string Party,
    decimal Amount,
    string Currency = "ETB",
    string? Notes = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "arrival.exception";
}

public class AssignLiabilityHandler : IRequestHandler<AssignLiabilityCommand, Result>
{
    private readonly ITenantDbContext _context;

    public AssignLiabilityHandler(ITenantDbContext context) => _context = context;

    public async Task<Result> Handle(AssignLiabilityCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<LiabilityParty>(request.Party, ignoreCase: true, out var party))
            return Result.Failure("Party must be Agency, Partner, Candidate, or Other", 400);

        var exceptionCase = await _context.ExceptionCases
            .FirstOrDefaultAsync(e => e.Id == request.ExceptionCaseId && !e.IsDeleted, ct);
        if (exceptionCase is null)
            return Result.Failure("Exception case not found", 404);
        if (exceptionCase.Status == ExceptionStatus.Closed)
            return Result.Failure("Cannot assign liability on a closed case", 400);

        _context.LiabilityAssignments.Add(new LiabilityAssignment
        {
            ExceptionCaseId = exceptionCase.Id,
            Party = party,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "ETB" : request.Currency.Trim().ToUpperInvariant(),
            Notes = request.Notes,
            AssignedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record CloseExceptionCommand(
    Guid ExceptionCaseId,
    string ResolutionSummary,
    decimal? FinancialImpactAmount = null,
    string? FinancialImpactCurrency = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "arrival.exception";
}

public class CloseExceptionHandler : IRequestHandler<CloseExceptionCommand, Result>
{
    private readonly ITenantDbContext _context;

    public CloseExceptionHandler(ITenantDbContext context) => _context = context;

    public async Task<Result> Handle(CloseExceptionCommand request, CancellationToken ct)
    {
        var exceptionCase = await _context.ExceptionCases
            .FirstOrDefaultAsync(e => e.Id == request.ExceptionCaseId && !e.IsDeleted, ct);
        if (exceptionCase is null)
            return Result.Failure("Exception case not found", 404);
        if (exceptionCase.Status == ExceptionStatus.Closed)
            return Result.Failure("Case is already closed", 400);

        exceptionCase.Status = ExceptionStatus.Closed;
        exceptionCase.ClosedAt = DateTime.UtcNow;
        exceptionCase.ResolutionSummary = request.ResolutionSummary.Trim();
        exceptionCase.FinancialImpactAmount = request.FinancialImpactAmount;
        exceptionCase.FinancialImpactCurrency = request.FinancialImpactCurrency;

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
