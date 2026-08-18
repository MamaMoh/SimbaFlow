using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Finance.Queries;

public record CommissionBoardRowDto(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    string PassportNumber,
    string Status,
    string? CountryOfTravel,
    string? PartnerName,
    DateTime OpenedAt,
    decimal TotalFeesAmount,
    decimal TotalPaidAmount,
    decimal BalanceAmount);

public record CommissionBoardResult(
    List<CommissionBoardRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record GetCommissionBoardQuery(
    int Page = 1,
    int PageSize = 50,
    string? Status = null,
    string? Country = null,
    string? Search = null) : IRequest<Result<CommissionBoardResult>>, IRequirePermission
{
    public string RequiredPermission => "commission.read";
}

public class GetCommissionBoardHandler
    : IRequestHandler<GetCommissionBoardQuery, Result<CommissionBoardResult>>
{
    private readonly ITenantDbContext _context;

    public GetCommissionBoardHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<CommissionBoardResult>> Handle(
        GetCommissionBoardQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query =
            from c in _context.Commissions.AsNoTracking()
            join cand in _context.Candidates.AsNoTracking() on c.CandidateId equals cand.Id
            where !c.IsDeleted && !cand.IsDeleted
            select new { Commission = c, Candidate = cand };

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<CommissionStatus>(request.Status, true, out var status))
            query = query.Where(x => x.Commission.Status == status);

        if (!string.IsNullOrWhiteSpace(request.Country))
        {
            var country = request.Country.Trim();
            query = query.Where(x =>
                (x.Commission.CountryOfTravel != null && x.Commission.CountryOfTravel.Contains(country))
                || (x.Candidate.CountryOfTravel != null && x.Candidate.CountryOfTravel.Contains(country)));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            query = query.Where(x =>
                x.Candidate.FirstName.ToLower().Contains(s)
                || x.Candidate.LastName.ToLower().Contains(s)
                || x.Candidate.PassportNumber.ToLower().Contains(s)
                || (x.Commission.PartnerName != null && x.Commission.PartnerName.ToLower().Contains(s)));
        }

        var total = await query.CountAsync(ct);

        var raw = await query
            .OrderBy(x => x.Commission.Status == CommissionStatus.Disputed ? 0
                : x.Commission.Status == CommissionStatus.Open ? 1
                : x.Commission.Status == CommissionStatus.Partial ? 2
                : 3)
            .ThenByDescending(x => x.Commission.OpenedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Commission.Id,
                x.Commission.CandidateId,
                x.Candidate.FirstName,
                x.Candidate.MiddleName,
                x.Candidate.LastName,
                x.Candidate.PassportNumber,
                Status = x.Commission.Status.ToString(),
                CountryOfTravel = x.Commission.CountryOfTravel ?? x.Candidate.CountryOfTravel,
                PartnerName = x.Commission.PartnerName ?? x.Candidate.PartnerName,
                x.Commission.OpenedAt,
                x.Commission.TotalFeesAmount,
                x.Commission.TotalPaidAmount,
                x.Commission.BalanceAmount
            })
            .ToListAsync(ct);

        var items = raw.Select(x => new CommissionBoardRowDto(
            x.Id,
            x.CandidateId,
            string.IsNullOrEmpty(x.MiddleName)
                ? $"{x.FirstName} {x.LastName}"
                : $"{x.FirstName} {x.MiddleName} {x.LastName}",
            x.PassportNumber,
            x.Status,
            x.CountryOfTravel,
            x.PartnerName,
            x.OpenedAt,
            x.TotalFeesAmount,
            x.TotalPaidAmount,
            x.BalanceAmount)).ToList();

        return Result<CommissionBoardResult>.Success(
            new CommissionBoardResult(items, total, page, pageSize));
    }
}

public record CommissionFeeDto(
    Guid Id,
    string FeeType,
    string? Description,
    decimal Amount,
    string Currency,
    decimal AmountEtb,
    int SortOrder);

public record CommissionPaymentDto(
    Guid Id,
    decimal Amount,
    string Currency,
    decimal ExchangeRateToEtb,
    decimal AmountEtb,
    DateTime PaidAt,
    string Method,
    string? Reference,
    string? Notes,
    Guid? JournalEntryId);

public record CommissionDisputeDto(
    Guid Id,
    string Status,
    string Reason,
    DateTime OpenedAt,
    DateTime? ResolvedAt,
    string? ResolutionNotes);

public record CommissionDetailDto(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    string PassportNumber,
    string Status,
    string? CountryOfTravel,
    string? PartnerName,
    DateTime OpenedAt,
    decimal TotalFeesAmount,
    decimal TotalPaidAmount,
    decimal BalanceAmount,
    List<CommissionFeeDto> Fees,
    List<CommissionPaymentDto> Payments,
    List<CommissionDisputeDto> Disputes);

public record GetCommissionByIdQuery(Guid Id)
    : IRequest<Result<CommissionDetailDto>>, IRequirePermission
{
    public string RequiredPermission => "commission.read";
}

public class GetCommissionByIdHandler
    : IRequestHandler<GetCommissionByIdQuery, Result<CommissionDetailDto>>
{
    private readonly ITenantDbContext _context;

    public GetCommissionByIdHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<CommissionDetailDto>> Handle(
        GetCommissionByIdQuery request, CancellationToken ct)
    {
        var row = await (
            from c in _context.Commissions.AsNoTracking()
            join cand in _context.Candidates.AsNoTracking() on c.CandidateId equals cand.Id
            where c.Id == request.Id && !c.IsDeleted && !cand.IsDeleted
            select new { Commission = c, Candidate = cand }).FirstOrDefaultAsync(ct);

        if (row is null)
            return Result<CommissionDetailDto>.Failure("Commission not found", 404);

        var fees = await _context.CommissionFees.AsNoTracking()
            .Where(f => f.CommissionId == request.Id && !f.IsDeleted)
            .OrderBy(f => f.SortOrder)
            .Select(f => new CommissionFeeDto(
                f.Id, f.FeeType.ToString(), f.Description, f.Amount, f.Currency, f.AmountEtb, f.SortOrder))
            .ToListAsync(ct);

        var payments = await _context.Payments.AsNoTracking()
            .Where(p => p.CommissionId == request.Id && !p.IsDeleted)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new CommissionPaymentDto(
                p.Id, p.Amount, p.Currency, p.ExchangeRateToEtb, p.AmountEtb, p.PaidAt,
                p.Method.ToString(), p.Reference, p.Notes, p.JournalEntryId))
            .ToListAsync(ct);

        var disputes = await _context.Disputes.AsNoTracking()
            .Where(d => d.CommissionId == request.Id && !d.IsDeleted)
            .OrderByDescending(d => d.OpenedAt)
            .Select(d => new CommissionDisputeDto(
                d.Id, d.Status.ToString(), d.Reason, d.OpenedAt, d.ResolvedAt, d.ResolutionNotes))
            .ToListAsync(ct);

        return Result<CommissionDetailDto>.Success(new CommissionDetailDto(
            row.Commission.Id,
            row.Commission.CandidateId,
            string.IsNullOrEmpty(row.Candidate.MiddleName)
                ? $"{row.Candidate.FirstName} {row.Candidate.LastName}"
                : $"{row.Candidate.FirstName} {row.Candidate.MiddleName} {row.Candidate.LastName}",
            row.Candidate.PassportNumber,
            row.Commission.Status.ToString(),
            row.Commission.CountryOfTravel ?? row.Candidate.CountryOfTravel,
            row.Commission.PartnerName ?? row.Candidate.PartnerName,
            row.Commission.OpenedAt,
            row.Commission.TotalFeesAmount,
            row.Commission.TotalPaidAmount,
            row.Commission.BalanceAmount,
            fees,
            payments,
            disputes));
    }
}

public record CommissionPartnerReportRowDto(
    string PartnerName,
    int Count,
    decimal TotalFeesEtb,
    decimal TotalPaidEtb,
    decimal BalanceEtb);

public record CommissionReportResult(
    List<CommissionPartnerReportRowDto> Rows,
    decimal GrandTotalFeesEtb,
    decimal GrandTotalPaidEtb,
    decimal GrandBalanceEtb);

public record GetCommissionReportQuery(
    DateOnly? From = null,
    DateOnly? To = null) : IRequest<Result<CommissionReportResult>>, IRequirePermission
{
    public string RequiredPermission => "commission.read";
}

public class GetCommissionReportHandler
    : IRequestHandler<GetCommissionReportQuery, Result<CommissionReportResult>>
{
    private readonly ITenantDbContext _context;

    public GetCommissionReportHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<CommissionReportResult>> Handle(
        GetCommissionReportQuery request, CancellationToken ct)
    {
        var query =
            from c in _context.Commissions.AsNoTracking()
            join cand in _context.Candidates.AsNoTracking() on c.CandidateId equals cand.Id
            where !c.IsDeleted && !cand.IsDeleted
            select new { Commission = c, Candidate = cand };

        if (request.From is DateOnly from)
        {
            var fromDt = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.Commission.OpenedAt >= fromDt);
        }

        if (request.To is DateOnly to)
        {
            var toDt = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.Commission.OpenedAt <= toDt);
        }

        var rows = await query
            .GroupBy(x => x.Commission.PartnerName ?? x.Candidate.PartnerName ?? "(Unspecified)")
            .Select(g => new CommissionPartnerReportRowDto(
                g.Key,
                g.Count(),
                g.Sum(x => x.Commission.TotalFeesAmount),
                g.Sum(x => x.Commission.TotalPaidAmount),
                g.Sum(x => x.Commission.BalanceAmount)))
            .OrderBy(r => r.PartnerName)
            .ToListAsync(ct);

        return Result<CommissionReportResult>.Success(new CommissionReportResult(
            rows,
            rows.Sum(r => r.TotalFeesEtb),
            rows.Sum(r => r.TotalPaidEtb),
            rows.Sum(r => r.BalanceEtb)));
    }
}
