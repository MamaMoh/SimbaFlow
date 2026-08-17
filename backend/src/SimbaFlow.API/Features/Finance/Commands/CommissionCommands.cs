using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

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

    public UpsertCommissionFeesHandler(ITenantDbContext context, IExchangeRateService fx)
    {
        _context = context;
        _fx = fx;
    }

    public async Task<Result> Handle(UpsertCommissionFeesCommand request, CancellationToken ct)
    {
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
                RecordedByUserId = userId
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

public record OpenDisputeCommand(Guid CommissionId, string Reason)
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

        var dispute = new Dispute
        {
            CommissionId = commission.Id,
            Status = DisputeStatus.Open,
            Reason = request.Reason.Trim(),
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

public record ResolveDisputeCommand(Guid DisputeId, string ResolutionNotes)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "commission.update";
}

public class ResolveDisputeHandler : IRequestHandler<ResolveDisputeCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ResolveDisputeHandler(ITenantDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
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

        dispute.Status = DisputeStatus.Resolved;
        dispute.ResolvedAt = DateTime.UtcNow;
        dispute.ResolutionNotes = request.ResolutionNotes.Trim();
        dispute.ResolvedByUserId = userId;

        CommissionFinanceHelpers.RecalcTotalsAndStatus(commission);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
