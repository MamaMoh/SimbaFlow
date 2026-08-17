using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Exceptions.Queries;

public record ExceptionCaseListItemDto(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    string PassportNumber,
    string Type,
    string Status,
    DateTime OpenedAt,
    decimal? FinancialImpactAmount,
    string? FinancialImpactCurrency);

public record ExceptionCaseListResult(
    List<ExceptionCaseListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record InvestigationNoteDto(
    Guid Id,
    Guid AuthorUserId,
    string Body,
    DateTime CreatedAt,
    Guid[] AttachmentDocumentIds);

public record LiabilityAssignmentDto(
    Guid Id,
    string Party,
    decimal Amount,
    string Currency,
    string? Notes,
    DateTime AssignedAt);

public record ExceptionCaseDetailDto(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    string PassportNumber,
    string Type,
    string Status,
    DateTime OpenedAt,
    Guid OpenedByUserId,
    DateTime? ClosedAt,
    string? ResolutionSummary,
    decimal? FinancialImpactAmount,
    string? FinancialImpactCurrency,
    List<InvestigationNoteDto> Notes,
    List<LiabilityAssignmentDto> Liabilities);

public record GetExceptionCasesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Type = null,
    Guid? OfficeId = null) : IRequest<Result<ExceptionCaseListResult>>, IRequirePermission
{
    public string RequiredPermission => "arrival.read";
}

public class GetExceptionCasesHandler : IRequestHandler<GetExceptionCasesQuery, Result<ExceptionCaseListResult>>
{
    private readonly ITenantDbContext _context;

    public GetExceptionCasesHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<ExceptionCaseListResult>> Handle(
        GetExceptionCasesQuery request, CancellationToken ct)
    {
        var query = _context.ExceptionCases.AsNoTracking().Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<ExceptionStatus>(request.Status, ignoreCase: true, out var status))
            query = query.Where(e => e.Status == status);

        if (!string.IsNullOrWhiteSpace(request.Type) &&
            Enum.TryParse<ExceptionType>(request.Type, ignoreCase: true, out var type))
            query = query.Where(e => e.Type == type);

        var joined = from e in query
            join c in _context.Candidates.AsNoTracking() on e.CandidateId equals c.Id
            where !c.IsDeleted
            select new { Case = e, Candidate = c };

        if (request.OfficeId.HasValue)
            joined = joined.Where(x => x.Candidate.OfficeId == request.OfficeId);

        var total = await joined.CountAsync(ct);
        var rows = await joined
            .OrderByDescending(x => x.Case.OpenedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = rows.Select(x => new ExceptionCaseListItemDto(
            x.Case.Id,
            x.Case.CandidateId,
            x.Candidate.FullName,
            x.Candidate.PassportNumber,
            x.Case.Type.ToString(),
            x.Case.Status.ToString(),
            x.Case.OpenedAt,
            x.Case.FinancialImpactAmount,
            x.Case.FinancialImpactCurrency)).ToList();

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return Result<ExceptionCaseListResult>.Success(
            new ExceptionCaseListResult(items, total, request.Page, request.PageSize, totalPages));
    }
}

public record GetExceptionCaseByIdQuery(Guid Id)
    : IRequest<Result<ExceptionCaseDetailDto>>, IRequirePermission
{
    public string RequiredPermission => "arrival.read";
}

public class GetExceptionCaseByIdHandler
    : IRequestHandler<GetExceptionCaseByIdQuery, Result<ExceptionCaseDetailDto>>
{
    private readonly ITenantDbContext _context;

    public GetExceptionCaseByIdHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<ExceptionCaseDetailDto>> Handle(
        GetExceptionCaseByIdQuery request, CancellationToken ct)
    {
        var exceptionCase = await _context.ExceptionCases
            .AsNoTracking()
            .Include(e => e.Notes)
            .Include(e => e.Liabilities)
            .FirstOrDefaultAsync(e => e.Id == request.Id && !e.IsDeleted, ct);
        if (exceptionCase is null)
            return Result<ExceptionCaseDetailDto>.Failure("Exception case not found", 404);

        var candidate = await _context.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == exceptionCase.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result<ExceptionCaseDetailDto>.Failure("Candidate not found", 404);

        var dto = new ExceptionCaseDetailDto(
            exceptionCase.Id,
            exceptionCase.CandidateId,
            candidate.FullName,
            candidate.PassportNumber,
            exceptionCase.Type.ToString(),
            exceptionCase.Status.ToString(),
            exceptionCase.OpenedAt,
            exceptionCase.OpenedByUserId,
            exceptionCase.ClosedAt,
            exceptionCase.ResolutionSummary,
            exceptionCase.FinancialImpactAmount,
            exceptionCase.FinancialImpactCurrency,
            exceptionCase.Notes
                .Where(n => !n.IsDeleted)
                .OrderBy(n => n.CreatedAt)
                .Select(n => new InvestigationNoteDto(
                    n.Id, n.AuthorUserId, n.Body, n.CreatedAt, n.AttachmentDocumentIds))
                .ToList(),
            exceptionCase.Liabilities
                .Where(l => !l.IsDeleted)
                .OrderBy(l => l.AssignedAt)
                .Select(l => new LiabilityAssignmentDto(
                    l.Id, l.Party.ToString(), l.Amount, l.Currency, l.Notes, l.AssignedAt))
                .ToList());

        return Result<ExceptionCaseDetailDto>.Success(dto);
    }
}
