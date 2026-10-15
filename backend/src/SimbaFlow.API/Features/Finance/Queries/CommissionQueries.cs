using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.API.Features.Candidates.Queries;

namespace SimbaFlow.API.Features.Finance.Queries;

public record CommissionBoardRowDto(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    string PassportNumber,
    string? LabourId,
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
    string? Search = null,
    string? Filters = null) : IRequest<Result<CommissionBoardResult>>, IRequirePermission
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

        // A row here is a commission and the candidate it is about, so the funnels are offered
        // to both halves before they meet. Each whitelist ignores the field names it does not
        // know, which is what lets one parsed list narrow two different tables.
        var funnels = TableFilters.Parse(request.Filters);

        var commissions = CommissionFilters.For()
            .Apply(_context.Commissions.AsNoTracking().Where(c => !c.IsDeleted), funnels);
        var people = CandidateFilters.Joined()
            .Apply(_context.Candidates.AsNoTracking().Where(c => !c.IsDeleted), funnels);

        var query =
            from c in commissions
            join cand in people on c.CandidateId equals cand.Id
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
                x.Candidate.LabourId,
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
            x.LabourId,
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
    Guid? JournalEntryId,
    string? ReceiptNumber,
    DateTime? VoidedAt,
    string? VoidReason);

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
    string? LabourId,
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
                p.Method.ToString(), p.Reference, p.Notes, p.JournalEntryId,
                p.ReceiptNumber, p.VoidedAt, p.VoidReason))
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
            row.Candidate.LabourId,
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

public record PaymentReceiptDto(
    Guid PaymentId,
    string ReceiptNumber,
    string AgencyName,
    string? AgencyAddress,
    string? AgencyPhone,
    string? AgencyLicenseNumber,
    string CandidateName,
    string PassportNumber,
    string? PartnerName,
    DateTime PaidAt,
    decimal Amount,
    string Currency,
    decimal ExchangeRateToEtb,
    decimal AmountEtb,
    string Method,
    string? Reference,
    string? Notes,
    string RecordedBy,
    decimal TotalFeesEtb,
    decimal TotalPaidEtb,
    decimal BalanceEtb,
    DateTime? VoidedAt,
    string? VoidReason);

public record GetPaymentReceiptQuery(Guid PaymentId)
    : IRequest<Result<PaymentReceiptDto>>, IRequirePermission
{
    public string RequiredPermission => "commission.read";
}

/// <summary>What the printed receipt shows. A voided payment still prints, marked void.</summary>
public class GetPaymentReceiptHandler : IRequestHandler<GetPaymentReceiptQuery, Result<PaymentReceiptDto>>
{
    private readonly ITenantDbContext _context;
    private readonly IPlatformDbContext _platform;
    private readonly ICurrentUserService _currentUser;

    public GetPaymentReceiptHandler(
        ITenantDbContext context, IPlatformDbContext platform, ICurrentUserService currentUser)
    {
        _context = context;
        _platform = platform;
        _currentUser = currentUser;
    }

    public async Task<Result<PaymentReceiptDto>> Handle(GetPaymentReceiptQuery request, CancellationToken ct)
    {
        var row = await (
            from p in _context.Payments.AsNoTracking()
            join c in _context.Commissions.AsNoTracking() on p.CommissionId equals c.Id
            join cand in _context.Candidates.AsNoTracking() on c.CandidateId equals cand.Id
            where p.Id == request.PaymentId && !p.IsDeleted && !c.IsDeleted
            select new { Payment = p, Commission = c, Candidate = cand }).FirstOrDefaultAsync(ct);

        if (row is null)
            return Result<PaymentReceiptDto>.Failure("Payment not found", 404);

        var tenant = _currentUser.TenantId is Guid tenantId
            ? await _platform.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            : null;
        var recordedBy = await _platform.ApplicationUsers.AsNoTracking()
            .Where(u => u.Id == row.Payment.RecordedByUserId)
            .Select(u => new { u.FirstName, u.MiddleName, u.LastName })
            .FirstOrDefaultAsync(ct);

        var pay = row.Payment;
        var person = row.Candidate;
        return Result<PaymentReceiptDto>.Success(new PaymentReceiptDto(
            pay.Id,
            pay.ReceiptNumber ?? pay.Id.ToString()[..8].ToUpperInvariant(),
            tenant?.Name ?? "",
            string.Join(", ", new[] { tenant?.Address, tenant?.City, tenant?.Country }
                .Where(x => !string.IsNullOrWhiteSpace(x))),
            tenant?.ContactPhone,
            tenant?.LicenseNumber,
            string.IsNullOrEmpty(person.MiddleName)
                ? $"{person.FirstName} {person.LastName}"
                : $"{person.FirstName} {person.MiddleName} {person.LastName}",
            person.PassportNumber,
            row.Commission.PartnerName ?? person.PartnerName,
            pay.PaidAt,
            pay.Amount,
            pay.Currency,
            pay.ExchangeRateToEtb,
            pay.AmountEtb,
            pay.Method.ToString(),
            pay.Reference,
            pay.Notes,
            recordedBy is null
                ? ""
                : string.Join(" ", new[] { recordedBy.FirstName, recordedBy.MiddleName, recordedBy.LastName }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
            row.Commission.TotalFeesAmount,
            row.Commission.TotalPaidAmount,
            row.Commission.BalanceAmount,
            pay.VoidedAt,
            pay.VoidReason));
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
