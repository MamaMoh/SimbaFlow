using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Accounting.Queries;

// ── Journal list ────────────────────────────────────────────────────────────

public record JournalListRowDto(
    Guid Id,
    string EntryNumber,
    DateTime PostedAt,
    string Description,
    string SourceType,
    Guid? SourceId,
    decimal Amount);

public record JournalListDto(List<JournalListRowDto> Items, int TotalCount, int Page, int PageSize);

public record GetJournalEntriesQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    string? SourceType = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<JournalListDto>>, IRequirePermission
{
    public string RequiredPermission => "accounting.read";
}

public class GetJournalEntriesHandler : IRequestHandler<GetJournalEntriesQuery, Result<JournalListDto>>
{
    private readonly ITenantDbContext _context;

    public GetJournalEntriesHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<JournalListDto>> Handle(GetJournalEntriesQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _context.JournalEntries.AsNoTracking().Where(j => !j.IsDeleted);
        if (request.From is DateOnly from)
        {
            var start = Ledger.StartOf(from);
            query = query.Where(j => j.PostedAt >= start);
        }
        if (request.To is DateOnly to)
        {
            var end = Ledger.EndOf(to);
            query = query.Where(j => j.PostedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(request.SourceType))
            query = query.Where(j => j.SourceType == request.SourceType);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(j => j.EntryNumber.ToLower().Contains(term) || j.Description.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(j => j.PostedAt)
            .ThenByDescending(j => j.EntryNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JournalListRowDto(
                j.Id, j.EntryNumber, j.PostedAt, j.Description, j.SourceType, j.SourceId,
                j.Lines.Where(l => !l.IsDeleted).Sum(l => l.Debit)))
            .ToListAsync(ct);

        return Result<JournalListDto>.Success(new JournalListDto(items, total, page, pageSize));
    }
}

// ── Shared ledger arithmetic ────────────────────────────────────────────────

public record AccountBalanceDto(
    Guid AccountId,
    string Code,
    string Name,
    string Type,
    decimal Debit,
    decimal Credit,
    // Signed the way the account normally runs: debit − credit for assets and expenses,
    // credit − debit for liabilities, equity and revenue.
    decimal Balance);

internal static class Ledger
{
    public static DateTime StartOf(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    public static DateTime EndOf(DateOnly day) => day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public static bool IsDebitNormal(AccountType type) => type is AccountType.Asset or AccountType.Expense;

    /// <summary>Posted totals per account between the dates (either end open), every account listed.</summary>
    public static async Task<List<AccountBalanceDto>> BalancesAsync(
        ITenantDbContext context, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var lines = context.JournalLines.AsNoTracking()
            .Where(l => !l.IsDeleted && !l.JournalEntry!.IsDeleted);
        if (from is DateOnly f)
        {
            var start = StartOf(f);
            lines = lines.Where(l => l.JournalEntry!.PostedAt >= start);
        }
        if (to is DateOnly t)
        {
            var end = EndOf(t);
            lines = lines.Where(l => l.JournalEntry!.PostedAt < end);
        }

        var totals = await lines
            .GroupBy(l => l.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(l => l.Debit), Credit = g.Sum(l => l.Credit) })
            .ToDictionaryAsync(x => x.AccountId, ct);

        var accounts = await context.Accounts.AsNoTracking()
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.Code)
            .ToListAsync(ct);

        return accounts
            .Where(a => a.IsActive || totals.ContainsKey(a.Id))
            .Select(a =>
            {
                totals.TryGetValue(a.Id, out var t);
                var debit = t?.Debit ?? 0;
                var credit = t?.Credit ?? 0;
                return new AccountBalanceDto(
                    a.Id, a.Code, a.Name, a.Type.ToString(), debit, credit,
                    IsDebitNormal(a.Type) ? debit - credit : credit - debit);
            })
            .ToList();
    }
}

// ── Trial balance ───────────────────────────────────────────────────────────

public record TrialBalanceRowDto(Guid AccountId, string Code, string Name, string Type, decimal Debit, decimal Credit);

public record TrialBalanceDto(DateOnly AsOf, List<TrialBalanceRowDto> Rows, decimal TotalDebit, decimal TotalCredit);

public record GetTrialBalanceQuery(DateOnly? AsOf = null) : IRequest<Result<TrialBalanceDto>>, IRequirePermission
{
    public string RequiredPermission => "accounting.read";
}

/// <summary>Each account's net balance on its debit or credit side; the two columns must agree.</summary>
public class GetTrialBalanceHandler : IRequestHandler<GetTrialBalanceQuery, Result<TrialBalanceDto>>
{
    private readonly ITenantDbContext _context;

    public GetTrialBalanceHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<TrialBalanceDto>> Handle(GetTrialBalanceQuery request, CancellationToken ct)
    {
        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var balances = await Ledger.BalancesAsync(_context, null, asOf, ct);

        var rows = balances
            .Select(b =>
            {
                var net = b.Debit - b.Credit;
                return new TrialBalanceRowDto(b.AccountId, b.Code, b.Name, b.Type,
                    net > 0 ? net : 0, net < 0 ? -net : 0);
            })
            .ToList();

        return Result<TrialBalanceDto>.Success(new TrialBalanceDto(
            asOf, rows, rows.Sum(r => r.Debit), rows.Sum(r => r.Credit)));
    }
}

// ── Profit and loss ─────────────────────────────────────────────────────────

public record StatementLineDto(string Code, string Name, decimal Amount);

public record ProfitAndLossDto(
    DateOnly From,
    DateOnly To,
    List<StatementLineDto> Revenue,
    decimal TotalRevenue,
    List<StatementLineDto> Expenses,
    decimal TotalExpenses,
    decimal NetProfit);

public record GetProfitAndLossQuery(DateOnly? From = null, DateOnly? To = null)
    : IRequest<Result<ProfitAndLossDto>>, IRequirePermission
{
    public string RequiredPermission => "accounting.read";
}

/// <summary>
/// Income and costs over a period. Waivers sit under revenue as a negative line, so revenue is
/// what the agency actually kept the right to collect.
/// </summary>
public class GetProfitAndLossHandler : IRequestHandler<GetProfitAndLossQuery, Result<ProfitAndLossDto>>
{
    private readonly ITenantDbContext _context;

    public GetProfitAndLossHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<ProfitAndLossDto>> Handle(GetProfitAndLossQuery request, CancellationToken ct)
    {
        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.From ?? new DateOnly(to.Year, to.Month, 1);
        if (from > to)
            return Result<ProfitAndLossDto>.Failure("From must be on or before To", 400);

        var balances = await Ledger.BalancesAsync(_context, from, to, ct);
        var revenue = Lines(balances, AccountType.Revenue);
        var expenses = Lines(balances, AccountType.Expense);
        var totalRevenue = revenue.Sum(l => l.Amount);
        var totalExpenses = expenses.Sum(l => l.Amount);

        return Result<ProfitAndLossDto>.Success(new ProfitAndLossDto(
            from, to, revenue, totalRevenue, expenses, totalExpenses, totalRevenue - totalExpenses));
    }

    private static List<StatementLineDto> Lines(List<AccountBalanceDto> balances, AccountType type) =>
        balances
            .Where(b => b.Type == type.ToString() && (b.Balance != 0 || b.Debit != 0))
            .Select(b => new StatementLineDto(b.Code, b.Name, b.Balance))
            .ToList();
}

// ── Balance sheet ───────────────────────────────────────────────────────────

public record BalanceSheetDto(
    DateOnly AsOf,
    List<StatementLineDto> Assets,
    decimal TotalAssets,
    List<StatementLineDto> Liabilities,
    decimal TotalLiabilities,
    List<StatementLineDto> Equity,
    decimal RetainedEarnings,
    decimal TotalEquity,
    decimal TotalLiabilitiesAndEquity);

public record GetBalanceSheetQuery(DateOnly? AsOf = null)
    : IRequest<Result<BalanceSheetDto>>, IRequirePermission
{
    public string RequiredPermission => "accounting.read";
}

/// <summary>
/// What the agency holds, what it owes, and what is left over, on a date. Profit to date (revenue
/// less expenses since the first entry) is shown as retained earnings so the two sides agree.
/// </summary>
public class GetBalanceSheetHandler : IRequestHandler<GetBalanceSheetQuery, Result<BalanceSheetDto>>
{
    private readonly ITenantDbContext _context;

    public GetBalanceSheetHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<BalanceSheetDto>> Handle(GetBalanceSheetQuery request, CancellationToken ct)
    {
        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var balances = await Ledger.BalancesAsync(_context, null, asOf, ct);

        List<StatementLineDto> Of(AccountType type) => balances
            .Where(b => b.Type == type.ToString() && b.Balance != 0)
            .Select(b => new StatementLineDto(b.Code, b.Name, b.Balance))
            .ToList();
        decimal Sum(AccountType type) => balances.Where(b => b.Type == type.ToString()).Sum(b => b.Balance);

        var assets = Of(AccountType.Asset);
        var liabilities = Of(AccountType.Liability);
        var equity = Of(AccountType.Equity);
        var retained = Sum(AccountType.Revenue) - Sum(AccountType.Expense);
        var totalAssets = Sum(AccountType.Asset);
        var totalLiabilities = Sum(AccountType.Liability);
        var totalEquity = Sum(AccountType.Equity) + retained;

        return Result<BalanceSheetDto>.Success(new BalanceSheetDto(
            asOf, assets, totalAssets, liabilities, totalLiabilities, equity, retained, totalEquity,
            totalLiabilities + totalEquity));
    }
}
