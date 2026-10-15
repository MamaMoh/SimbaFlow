using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Features.Accounting.Commands;

public record RecordExpenseCommand(
    DateOnly ExpenseDate,
    string Category,
    string Description,
    decimal Amount,
    string? Currency,
    string Method,
    string? Payee,
    string? Reference,
    Guid? CommissionId) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "accounting.post";
}

/// <summary>
/// Records money paid out and posts it: Dr the category's expense account (or, for a medical,
/// ticket or partner payout, the payable it settles) / Cr cash.
/// </summary>
public class RecordExpenseHandler : IRequestHandler<RecordExpenseCommand, Result<Guid>>
{
    private readonly ITenantDbContext _context;
    private readonly IExchangeRateService _fx;
    private readonly IJournalPostingService _journal;
    private readonly ICurrentUserService _currentUser;

    public RecordExpenseHandler(
        ITenantDbContext context,
        IExchangeRateService fx,
        IJournalPostingService journal,
        ICurrentUserService currentUser)
    {
        _context = context;
        _fx = fx;
        _journal = journal;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(RecordExpenseCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result<Guid>.Failure("Unauthenticated", 401);
        if (request.Amount <= 0)
            return Result<Guid>.Failure("Amount must be > 0", 400);
        if (string.IsNullOrWhiteSpace(request.Description))
            return Result<Guid>.Failure("Description is required", 400);
        if (!Enum.TryParse<ExpenseCategory>(request.Category, true, out var category)
            || !Enum.IsDefined(category))
            return Result<Guid>.Failure($"Invalid category: {request.Category}", 400);
        if (!Enum.TryParse<PaymentMethod>(request.Method, true, out var method))
            return Result<Guid>.Failure($"Invalid payment method: {request.Method}", 400);

        if (request.CommissionId is Guid commissionId
            && !await _context.Commissions.AnyAsync(c => c.Id == commissionId && !c.IsDeleted, ct))
            return Result<Guid>.Failure("Commission not found", 404);

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "ETB" : request.Currency.Trim().ToUpperInvariant();
        decimal rate;
        try
        {
            rate = await _fx.ResolveRateToEtbAsync(currency, request.ExpenseDate, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Guid>.Failure(ex.Message, 400);
        }

        var expense = new Expense
        {
            ExpenseDate = request.ExpenseDate,
            Category = category,
            Description = request.Description.Trim(),
            Payee = request.Payee?.Trim(),
            Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            Currency = currency,
            ExchangeRateToEtb = rate,
            AmountEtb = Math.Round(request.Amount * rate, 2, MidpointRounding.AwayFromZero),
            Method = method,
            Reference = request.Reference?.Trim(),
            CommissionId = request.CommissionId,
            RecordedByUserId = userId,
        };
        _context.Expenses.Add(expense);

        var entry = await _journal.PostAsync(
            $"Expense: {expense.Description}",
            JournalPostingService.SourceTypeExpense,
            expense.Id,
            [
                new JournalLineSpec(LedgerAccounts.ForExpense(category), expense.AmountEtb, 0, expense.Payee),
                new JournalLineSpec(LedgerAccounts.CashBank, 0, expense.AmountEtb, expense.Payee),
            ],
            userId,
            request.ExpenseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ct);
        expense.JournalEntryId = entry?.Id;

        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(expense.Id, 201);
    }
}

public record VoidExpenseCommand(Guid ExpenseId, string Reason) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "accounting.post";
}

/// <summary>Takes an expense back with a reversing entry; the expense stays on record, marked void.</summary>
public class VoidExpenseHandler : IRequestHandler<VoidExpenseCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IJournalPostingService _journal;
    private readonly ICurrentUserService _currentUser;

    public VoidExpenseHandler(ITenantDbContext context, IJournalPostingService journal, ICurrentUserService currentUser)
    {
        _context = context;
        _journal = journal;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(VoidExpenseCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure("A reason is required to void an expense", 400);

        var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == request.ExpenseId && !e.IsDeleted, ct);
        if (expense is null)
            return Result.Failure("Expense not found", 404);
        if (expense.VoidedAt is not null)
            return Result.Failure("Expense is already voided", 400);

        if (expense.JournalEntryId is Guid entryId)
        {
            var reversal = await _journal.ReverseAsync(
                entryId, $"Void of expense: {expense.Description}",
                JournalPostingService.SourceTypeExpenseVoid, expense.Id, userId, ct);
            expense.VoidJournalEntryId = reversal.Id;
        }

        expense.VoidedAt = DateTime.UtcNow;
        expense.VoidedByUserId = userId;
        expense.VoidReason = request.Reason.Trim();
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
