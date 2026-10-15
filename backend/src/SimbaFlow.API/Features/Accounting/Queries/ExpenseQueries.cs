using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Accounting.Queries;

public record ExpenseDto(
    Guid Id,
    DateOnly ExpenseDate,
    string Category,
    string Description,
    string? Payee,
    decimal Amount,
    string Currency,
    decimal ExchangeRateToEtb,
    decimal AmountEtb,
    string Method,
    string? Reference,
    Guid? CommissionId,
    Guid? JournalEntryId,
    DateTime? VoidedAt,
    string? VoidReason);

public record GetExpensesQuery(DateOnly? From = null, DateOnly? To = null, string? Category = null)
    : IRequest<Result<List<ExpenseDto>>>, IRequirePermission
{
    public string RequiredPermission => "accounting.read";
}

public class GetExpensesHandler : IRequestHandler<GetExpensesQuery, Result<List<ExpenseDto>>>
{
    private readonly ITenantDbContext _context;

    public GetExpensesHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<List<ExpenseDto>>> Handle(GetExpensesQuery request, CancellationToken ct)
    {
        var query = _context.Expenses.AsNoTracking().Where(e => !e.IsDeleted);
        if (request.From is DateOnly from) query = query.Where(e => e.ExpenseDate >= from);
        if (request.To is DateOnly to) query = query.Where(e => e.ExpenseDate <= to);
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            if (!Enum.TryParse<ExpenseCategory>(request.Category, true, out var category))
                return Result<List<ExpenseDto>>.Failure($"Invalid category: {request.Category}", 400);
            query = query.Where(e => e.Category == category);
        }

        var items = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .Take(2000)
            .Select(e => new ExpenseDto(
                e.Id, e.ExpenseDate, e.Category.ToString(), e.Description, e.Payee, e.Amount, e.Currency,
                e.ExchangeRateToEtb, e.AmountEtb, e.Method.ToString(), e.Reference, e.CommissionId,
                e.JournalEntryId, e.VoidedAt, e.VoidReason))
            .ToListAsync(ct);

        return Result<List<ExpenseDto>>.Success(items);
    }
}
