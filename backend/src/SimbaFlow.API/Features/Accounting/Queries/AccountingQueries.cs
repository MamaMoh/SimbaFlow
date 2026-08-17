using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Accounting.Queries;

public record AccountDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    string Currency,
    bool IsSystem,
    bool IsActive);

public record GetAccountsQuery(bool? ActiveOnly = true)
    : IRequest<Result<List<AccountDto>>>, IRequirePermission
{
    public string RequiredPermission => "accounting.read";
}

public class GetAccountsHandler : IRequestHandler<GetAccountsQuery, Result<List<AccountDto>>>
{
    private readonly ITenantDbContext _context;

    public GetAccountsHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<List<AccountDto>>> Handle(GetAccountsQuery request, CancellationToken ct)
    {
        var query = _context.Accounts.AsNoTracking().Where(a => !a.IsDeleted);
        if (request.ActiveOnly != false)
            query = query.Where(a => a.IsActive);

        var items = await query
            .OrderBy(a => a.Code)
            .Select(a => new AccountDto(
                a.Id, a.Code, a.Name, a.Type.ToString(), a.Currency, a.IsSystem, a.IsActive))
            .ToListAsync(ct);

        return Result<List<AccountDto>>.Success(items);
    }
}

public record JournalLineDto(
    Guid Id,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal Debit,
    decimal Credit,
    string? Memo);

public record JournalEntryDto(
    Guid Id,
    string EntryNumber,
    DateTime PostedAt,
    string Description,
    string SourceType,
    Guid? SourceId,
    Guid PostedByUserId,
    List<JournalLineDto> Lines,
    decimal TotalDebit,
    decimal TotalCredit);

public record GetJournalEntryByIdQuery(Guid Id)
    : IRequest<Result<JournalEntryDto>>, IRequirePermission
{
    public string RequiredPermission => "accounting.read";
}

public class GetJournalEntryByIdHandler
    : IRequestHandler<GetJournalEntryByIdQuery, Result<JournalEntryDto>>
{
    private readonly ITenantDbContext _context;

    public GetJournalEntryByIdHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<JournalEntryDto>> Handle(
        GetJournalEntryByIdQuery request, CancellationToken ct)
    {
        var entry = await _context.JournalEntries.AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == request.Id && !j.IsDeleted, ct);

        if (entry is null)
            return Result<JournalEntryDto>.Failure("Journal entry not found", 404);

        var accountIds = entry.Lines.Where(l => !l.IsDeleted).Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _context.Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var lines = entry.Lines
            .Where(l => !l.IsDeleted)
            .Select(l =>
            {
                accounts.TryGetValue(l.AccountId, out var acct);
                return new JournalLineDto(
                    l.Id,
                    l.AccountId,
                    acct?.Code ?? "",
                    acct?.Name ?? "",
                    l.Debit,
                    l.Credit,
                    l.Memo);
            })
            .ToList();

        return Result<JournalEntryDto>.Success(new JournalEntryDto(
            entry.Id,
            entry.EntryNumber,
            entry.PostedAt,
            entry.Description,
            entry.SourceType,
            entry.SourceId,
            entry.PostedByUserId,
            lines,
            lines.Sum(l => l.Debit),
            lines.Sum(l => l.Credit)));
    }
}

public record ExchangeRateDto(
    Guid Id,
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateOnly EffectiveDate,
    string? Source);

public record GetExchangeRatesQuery(
    string? FromCurrency = null,
    string? ToCurrency = null,
    DateOnly? AsOf = null,
    int Take = 100) : IRequest<Result<List<ExchangeRateDto>>>, IRequirePermission
{
    public string RequiredPermission => "accounting.read";
}

public class GetExchangeRatesHandler
    : IRequestHandler<GetExchangeRatesQuery, Result<List<ExchangeRateDto>>>
{
    private readonly IPlatformDbContext _platform;

    public GetExchangeRatesHandler(IPlatformDbContext platform) => _platform = platform;

    public async Task<Result<List<ExchangeRateDto>>> Handle(
        GetExchangeRatesQuery request, CancellationToken ct)
    {
        var take = Math.Clamp(request.Take, 1, 500);
        var query = _platform.ExchangeRates.AsNoTracking().Where(r => !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.FromCurrency))
        {
            var from = request.FromCurrency.Trim().ToUpperInvariant();
            query = query.Where(r => r.FromCurrency == from);
        }

        if (!string.IsNullOrWhiteSpace(request.ToCurrency))
        {
            var to = request.ToCurrency.Trim().ToUpperInvariant();
            query = query.Where(r => r.ToCurrency == to);
        }

        if (request.AsOf is DateOnly asOf)
            query = query.Where(r => r.EffectiveDate <= asOf);

        var items = await query
            .OrderByDescending(r => r.EffectiveDate)
            .ThenBy(r => r.FromCurrency)
            .Take(take)
            .Select(r => new ExchangeRateDto(
                r.Id, r.FromCurrency, r.ToCurrency, r.Rate, r.EffectiveDate, r.Source))
            .ToListAsync(ct);

        return Result<List<ExchangeRateDto>>.Success(items);
    }
}
