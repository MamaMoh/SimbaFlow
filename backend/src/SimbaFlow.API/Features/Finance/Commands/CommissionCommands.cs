using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Features.Finance.Commands;

public record FeeLineInput(
    string FeeType,
    string? Description,
    decimal Amount,
    string? Currency,
    int SortOrder = 0);

public record UpsertCommissionFeesCommand(Guid CommissionId, List<FeeLineInput> Fees)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "commission.update";
}

public class UpsertCommissionFeesHandler : IRequestHandler<UpsertCommissionFeesCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IExchangeRateService _fx;
    private readonly IJournalPostingService _journal;
    private readonly ICurrentUserService _currentUser;

    public UpsertCommissionFeesHandler(
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

    public async Task<Result> Handle(UpsertCommissionFeesCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var commission = await _context.Commissions
            .Include(c => c.Fees)
            .Include(c => c.Payments)
            .Include(c => c.Disputes)
            .FirstOrDefaultAsync(c => c.Id == request.CommissionId && !c.IsDeleted, ct);

        if (commission is null)
            return Result.Failure("Commission not found", 404);

        if (commission.Status == CommissionStatus.Settled)
            return Result.Failure("Cannot edit fees on a Settled commission", 400);

        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var incoming = request.Fees ?? [];

        // Soft-delete existing fee lines via DbSet (avoids InMemory graph/concurrency quirks)
        var existingFees = await _context.CommissionFees
            .Where(f => f.CommissionId == commission.Id && !f.IsDeleted)
            .ToListAsync(ct);
        foreach (var existing in existingFees)
            existing.IsDeleted = true;

        var sort = 0;
        var newFees = new List<CommissionFee>();
        foreach (var line in incoming.OrderBy(f => f.SortOrder))
        {
            if (!Enum.TryParse<FeeType>(line.FeeType, true, out var feeType))
                return Result.Failure($"Invalid fee type: {line.FeeType}", 400);

            if (line.Amount < 0)
                return Result.Failure("Fee amount must be ≥ 0", 400);

            var currency = string.IsNullOrWhiteSpace(line.Currency) ? "ETB" : line.Currency.Trim().ToUpperInvariant();
            decimal amountEtb;
            try
            {
                amountEtb = await _fx.ConvertToEtbAsync(line.Amount, currency, asOf, ct);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(ex.Message, 400);
            }

            var fee = new CommissionFee
            {
                CommissionId = commission.Id,
                FeeType = feeType,
                Description = line.Description?.Trim(),
                Amount = Math.Round(line.Amount, 2, MidpointRounding.AwayFromZero),
                Currency = currency,
                AmountEtb = amountEtb,
                SortOrder = line.SortOrder != 0 ? line.SortOrder : sort++
            };
            newFees.Add(fee);
            _context.CommissionFees.Add(fee);
        }

        // Keep navigation in sync for recalc
        commission.Fees = existingFees.Concat(newFees).ToList();
        CommissionFinanceHelpers.RecalcTotalsAndStatus(commission);

        // Charging fees is when the agency earns its fee and starts owing the pass-through money.
        await _journal.SyncCommissionFeesAsync(commission, userId, cancellationToken: ct);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record RecordPaymentCommand(
    Guid CommissionId,
    decimal Amount,
    string? Currency,
    string Method,
    DateTime? PaidAt,
    string? Reference,
    string? Notes) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "accounting.post";
}

public class RecordPaymentHandler : IRequestHandler<RecordPaymentCommand, Result<Guid>>
{
    public const decimal OverpaymentTolerance = 1m;

    private readonly ITenantDbContext _context;
    private readonly IExchangeRateService _fx;
    private readonly IJournalPostingService _journal;
    private readonly ICurrentUserService _currentUser;

    public RecordPaymentHandler(
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

    public async Task<Result<Guid>> Handle(RecordPaymentCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result<Guid>.Failure("Unauthenticated", 401);

        if (request.Amount <= 0)
            return Result<Guid>.Failure("Payment amount must be > 0", 400);

        if (!Enum.TryParse<PaymentMethod>(request.Method, true, out var method))
            return Result<Guid>.Failure($"Invalid payment method: {request.Method}", 400);

        var commission = await _context.Commissions
            .Include(c => c.Fees)
            .Include(c => c.Payments)
            .Include(c => c.Disputes)
            .FirstOrDefaultAsync(c => c.Id == request.CommissionId && !c.IsDeleted, ct);

        if (commission is null)
            return Result<Guid>.Failure("Commission not found", 404);

        if (!commission.Fees.Any(f => !f.IsDeleted))
            return Result<Guid>.Failure("Record fees before accepting a payment", 400);

        if (commission.Status == CommissionStatus.Settled && commission.BalanceAmount <= 0)
            return Result<Guid>.Failure("Commission is already Settled", 400);

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? "ETB"
            : request.Currency.Trim().ToUpperInvariant();
        var paidAt = request.PaidAt?.ToUniversalTime() ?? DateTime.UtcNow;
        var asOf = DateOnly.FromDateTime(paidAt);

        decimal rate;
        decimal amountEtb;
        try
        {
            rate = await _fx.ResolveRateToEtbAsync(currency, asOf, ct);
            amountEtb = Math.Round(request.Amount * rate, 2, MidpointRounding.AwayFromZero);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Guid>.Failure(ex.Message, 400);
        }

        if (amountEtb <= 0)
            return Result<Guid>.Failure("Converted ETB amount must be > 0", 400);

        // A payment above what is owed would leave the candidate in credit with nothing to show
        // for it. Up to one birr over is let through so a foreign-currency payment of the full
        // balance is not refused for its rounding.
        CommissionFinanceHelpers.RecalcTotalsAndStatus(commission);
        if (amountEtb > commission.BalanceAmount + OverpaymentTolerance)
            return Result<Guid>.Failure(
                $"This payment ({amountEtb:N2} ETB) is more than the balance owed ({commission.BalanceAmount:N2} ETB).",
                400);

        if (_context is not TenantDbContext db)
            return Result<Guid>.Failure("Database transaction unavailable", 500);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var payment = new Payment
            {
                CommissionId = commission.Id,
                Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
                Currency = currency,
                ExchangeRateToEtb = rate,
                AmountEtb = amountEtb,
                PaidAt = paidAt,
                Method = method,
                Reference = request.Reference?.Trim(),
                Notes = request.Notes?.Trim(),
                RecordedByUserId = userId,
                ReceiptNumber = await _journal.AllocateReceiptNumberAsync(ct)
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(ct);

            await _journal.PostCommissionPaymentAsync(payment, commission, userId, ct);
            CommissionFinanceHelpers.RecalcTotalsAndStatus(commission);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Result<Guid>.Success(payment.Id, 201);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}

public record VoidPaymentCommand(Guid PaymentId, string Reason)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "accounting.post";
}

/// <summary>
/// Takes a payment back: posts the reverse of its journal entry, marks it voided with who and
/// why, and recalculates what the candidate owes. Nothing is deleted.
/// </summary>
public class VoidPaymentHandler : IRequestHandler<VoidPaymentCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IJournalPostingService _journal;
    private readonly ICurrentUserService _currentUser;

    public VoidPaymentHandler(
        ITenantDbContext context, IJournalPostingService journal, ICurrentUserService currentUser)
    {
        _context = context;
        _journal = journal;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(VoidPaymentCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure("A reason is required to void a payment", 400);

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId && !p.IsDeleted, ct);
        if (payment is null)
            return Result.Failure("Payment not found", 404);
        if (payment.VoidedAt is not null)
            return Result.Failure("Payment is already voided", 400);

        var commission = await _context.Commissions
            .Include(c => c.Fees)
            .Include(c => c.Payments)
            .Include(c => c.Disputes)
            .FirstOrDefaultAsync(c => c.Id == payment.CommissionId && !c.IsDeleted, ct);
        if (commission is null)
            return Result.Failure("Commission not found", 404);

        if (payment.JournalEntryId is Guid entryId)
        {
            var reversal = await _journal.ReverseAsync(
                entryId,
                $"Void of payment {payment.ReceiptNumber ?? payment.Id.ToString()}",
                JournalPostingService.SourceTypePaymentVoid,
                payment.Id,
                userId,
                ct);
            payment.VoidJournalEntryId = reversal.Id;
        }

        payment.VoidedAt = DateTime.UtcNow;
        payment.VoidedByUserId = userId;
        payment.VoidReason = request.Reason.Trim();

        CommissionFinanceHelpers.RecalcTotalsAndStatus(commission);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record OpenDisputeCommand(Guid CommissionId, string Reason, decimal? AmountEtb = null)
    : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "commission.update";
}

public class OpenDisputeHandler : IRequestHandler<OpenDisputeCommand, Result<Guid>>
{
    private readonly ITenantDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public OpenDisputeHandler(ITenantDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(OpenDisputeCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result<Guid>.Failure("Unauthenticated", 401);

        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<Guid>.Failure("Dispute reason is required", 400);

        var commission = await _context.Commissions
            .Include(c => c.Fees)
            .Include(c => c.Payments)
            .Include(c => c.Disputes)
            .FirstOrDefaultAsync(c => c.Id == request.CommissionId && !c.IsDeleted, ct);

        if (commission is null)
            return Result<Guid>.Failure("Commission not found", 404);

        if (commission.Disputes.Any(d => !d.IsDeleted && d.Status == DisputeStatus.Open))
            return Result<Guid>.Failure("An open dispute already exists for this commission", 409);

        decimal? amount = request.AmountEtb is decimal a
            ? Math.Round(a, 2, MidpointRounding.AwayFromZero)
            : null;
        if (amount is <= 0)
            return Result<Guid>.Failure("The disputed amount must be greater than 0", 400);
        if (amount > commission.BalanceAmount)
            return Result<Guid>.Failure(
                $"The disputed amount is more than the balance owed ({commission.BalanceAmount:N2} ETB)", 400);

        var dispute = new Dispute
        {
            CommissionId = commission.Id,
            Status = DisputeStatus.Open,
            Reason = request.Reason.Trim(),
            DisputedAmountEtb = amount,
            OpenedAt = DateTime.UtcNow,
            OpenedByUserId = userId
        };

        _context.Disputes.Add(dispute);
        commission.Disputes.Add(dispute);
        CommissionFinanceHelpers.RecalcTotalsAndStatus(commission);
        await _context.SaveChangesAsync(ct);

        return Result<Guid>.Success(dispute.Id, 201);
    }
}

/// <param name="Outcome">Enforced (default), Waived or Adjusted.</param>
/// <param name="AdjustmentAmountEtb">For Adjusted: how much of the balance to write off.</param>
public record ResolveDisputeCommand(
    Guid DisputeId,
    string ResolutionNotes,
    string? Outcome = null,
    decimal? AdjustmentAmountEtb = null)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "commission.update";
}

/// <summary>
/// Closes a dispute with its outcome. Waived and Adjusted write money off the balance and post
/// Dr fee waivers (4900) / Cr receivable; Enforced leaves the balance as it is.
/// </summary>
public class ResolveDisputeHandler : IRequestHandler<ResolveDisputeCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IJournalPostingService _journal;
    private readonly ICurrentUserService _currentUser;

    public ResolveDisputeHandler(
        ITenantDbContext context, IJournalPostingService journal, ICurrentUserService currentUser)
    {
        _context = context;
        _journal = journal;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ResolveDisputeCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        if (string.IsNullOrWhiteSpace(request.ResolutionNotes))
            return Result.Failure("Resolution notes are required", 400);

        var dispute = await _context.Disputes
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId && !d.IsDeleted, ct);

        if (dispute is null)
            return Result.Failure("Dispute not found", 404);

        if (dispute.Status != DisputeStatus.Open)
            return Result.Failure("Dispute is not open", 400);

        var commission = await _context.Commissions
            .Include(c => c.Fees)
            .Include(c => c.Payments)
            .Include(c => c.Disputes)
            .FirstOrDefaultAsync(c => c.Id == dispute.CommissionId && !c.IsDeleted, ct);

        if (commission is null)
            return Result.Failure("Commission not found", 404);

        var outcome = DisputeOutcome.Enforced;
        if (!string.IsNullOrWhiteSpace(request.Outcome)
            && !Enum.TryParse(request.Outcome, true, out outcome))
            return Result.Failure($"Invalid outcome: {request.Outcome}", 400);

        CommissionFinanceHelpers.RecalcTotalsAndStatus(commission);
        var balance = Math.Max(0, commission.BalanceAmount);
        decimal writeOff;
        switch (outcome)
        {
            case DisputeOutcome.Waived:
                writeOff = Math.Min(dispute.DisputedAmountEtb ?? balance, balance);
                break;
            case DisputeOutcome.Adjusted:
                if (request.AdjustmentAmountEtb is not decimal adj || adj <= 0)
                    return Result.Failure("An adjusted outcome needs the amount to write off", 400);
                writeOff = Math.Round(adj, 2, MidpointRounding.AwayFromZero);
                if (writeOff > balance)
                    return Result.Failure(
                        $"The adjustment is more than the balance owed ({balance:N2} ETB)", 400);
                break;
            default:
                writeOff = 0;
                break;
        }

        if (writeOff > 0)
        {
            var entry = await _journal.PostAsync(
                $"Dispute {outcome.ToString().ToLowerInvariant()}: commission {commission.Id}",
                JournalPostingService.SourceTypeDisputeAdjustment,
                dispute.Id,
                [
                    new JournalLineSpec(LedgerAccounts.FeeWaivers, writeOff, 0, dispute.Reason),
                    new JournalLineSpec(LedgerAccounts.Receivable, 0, writeOff, dispute.Reason),
                ],
                userId,
                cancellationToken: ct);
            dispute.AdjustmentJournalEntryId = entry?.Id;
        }

        dispute.Status = DisputeStatus.Resolved;
        dispute.Outcome = outcome;
        dispute.AdjustmentAmountEtb = writeOff;
        dispute.ResolvedAt = DateTime.UtcNow;
        dispute.ResolutionNotes = request.ResolutionNotes.Trim();
        dispute.ResolvedByUserId = userId;

        CommissionFinanceHelpers.RecalcTotalsAndStatus(commission);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
