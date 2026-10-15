using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.Infrastructure.Services;

public sealed class JournalPostingService : IJournalPostingService
{
    public const string SourceTypeCommissionPayment = "CommissionPayment";
    public const string SourceTypeCommissionFees = "CommissionFees";
    public const string SourceTypePaymentVoid = "CommissionPaymentVoid";
    public const string SourceTypeDisputeAdjustment = "DisputeAdjustment";

    /// <summary>
    /// Payments posted before fees were accrued credited revenue directly. The backfill reverses
    /// that credit against the receivable once, keyed by the payment.
    /// </summary>
    public const string SourceTypeLegacyPaymentReclass = "LegacyPaymentReclass";

    private readonly ITenantDbContext _context;
    private readonly ILogger<JournalPostingService> _logger;

    public JournalPostingService(ITenantDbContext context, ILogger<JournalPostingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<JournalEntry?> PostAsync(
        string description,
        string sourceType,
        Guid? sourceId,
        IReadOnlyList<JournalLineSpec> lines,
        Guid postedByUserId,
        DateTime? postedAt = null,
        CancellationToken cancellationToken = default)
    {
        var rounded = lines
            .Select(l => l with
            {
                Debit = Math.Round(l.Debit, 2, MidpointRounding.AwayFromZero),
                Credit = Math.Round(l.Credit, 2, MidpointRounding.AwayFromZero),
            })
            .Where(l => l.Debit != 0 || l.Credit != 0)
            .ToList();

        if (rounded.Count == 0)
            return null;

        if (rounded.Any(l => l.Debit < 0 || l.Credit < 0 || (l.Debit != 0 && l.Credit != 0)))
            throw new InvalidOperationException("Each journal line must be a positive debit or a positive credit.");

        var debitSum = rounded.Sum(l => l.Debit);
        var creditSum = rounded.Sum(l => l.Credit);
        if (debitSum != creditSum)
            throw new InvalidOperationException(
                $"Unbalanced journal: debit {debitSum} != credit {creditSum}.");

        var codes = rounded.Select(l => l.AccountCode).Distinct().ToList();
        var accounts = await _context.Accounts
            .Where(a => codes.Contains(a.Code) && !a.IsDeleted && a.IsActive)
            .ToDictionaryAsync(a => a.Code, cancellationToken);
        var missing = codes.Where(c => !accounts.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Account(s) {string.Join(", ", missing)} missing from the chart of accounts.");

        var entry = new JournalEntry
        {
            EntryNumber = await AllocateEntryNumberAsync(cancellationToken),
            PostedAt = postedAt ?? DateTime.UtcNow,
            Description = description,
            SourceType = sourceType,
            SourceId = sourceId,
            PostedByUserId = postedByUserId,
            Lines = rounded.Select(l => new JournalLine
            {
                AccountId = accounts[l.AccountCode].Id,
                Debit = l.Debit,
                Credit = l.Credit,
                Memo = l.Memo,
            }).ToList(),
        };

        _context.JournalEntries.Add(entry);

        _logger.LogInformation(
            "Posted journal {EntryNumber} ({SourceType} {SourceId}) for {Amount} ETB",
            entry.EntryNumber, sourceType, sourceId, debitSum);

        return entry;
    }

    public async Task<JournalEntry?> SyncCommissionFeesAsync(
        Commission commission,
        Guid postedByUserId,
        DateTime? postedAt = null,
        CancellationToken cancellationToken = default)
    {
        // What earlier fee entries credited to each account, net.
        var postedLines = await _context.JournalLines.AsNoTracking()
            .Where(l => !l.IsDeleted
                && !l.JournalEntry!.IsDeleted
                && l.JournalEntry.SourceType == SourceTypeCommissionFees
                && l.JournalEntry.SourceId == commission.Id)
            .Select(l => new { l.Account!.Code, l.Debit, l.Credit })
            .ToListAsync(cancellationToken);

        var posted = postedLines
            .Where(l => l.Code != LedgerAccounts.Receivable)
            .GroupBy(l => l.Code)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Credit - l.Debit));

        var target = (commission.Fees ?? [])
            .Where(f => !f.IsDeleted)
            .GroupBy(f => LedgerAccounts.ForFee(f.FeeType))
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(f => f.AmountEtb), 2, MidpointRounding.AwayFromZero));

        var lines = new List<JournalLineSpec>();
        foreach (var code in posted.Keys.Union(target.Keys).Order())
        {
            var delta = target.GetValueOrDefault(code) - posted.GetValueOrDefault(code);
            if (delta > 0) lines.Add(new JournalLineSpec(code, 0, delta, "Fees charged"));
            else if (delta < 0) lines.Add(new JournalLineSpec(code, -delta, 0, "Fees reduced"));
        }

        var net = lines.Sum(l => l.Credit - l.Debit);
        if (net > 0) lines.Insert(0, new JournalLineSpec(LedgerAccounts.Receivable, net, 0, "Owed by candidate"));
        else if (net < 0) lines.Insert(0, new JournalLineSpec(LedgerAccounts.Receivable, 0, -net, "Owed by candidate"));

        return await PostAsync(
            $"Fees for commission {commission.Id}",
            SourceTypeCommissionFees,
            commission.Id,
            lines,
            postedByUserId,
            postedAt,
            cancellationToken);
    }

    public async Task<JournalEntry> PostCommissionPaymentAsync(
        Payment payment,
        Commission commission,
        Guid postedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (payment.AmountEtb <= 0)
            throw new InvalidOperationException("Payment AmountEtb must be positive to post a journal.");

        var amount = Math.Round(payment.AmountEtb, 2, MidpointRounding.AwayFromZero);
        var memo = $"Payment {payment.Id}";

        var entry = await PostAsync(
            $"Commission payment {commission.Id}",
            SourceTypeCommissionPayment,
            payment.Id,
            [
                new JournalLineSpec(LedgerAccounts.CashBank, amount, 0, memo),
                new JournalLineSpec(LedgerAccounts.Receivable, 0, amount, memo),
            ],
            postedByUserId,
            payment.PaidAt == default ? null : payment.PaidAt,
            cancellationToken);

        payment.JournalEntryId = entry!.Id;
        return entry;
    }

    public async Task<JournalEntry> ReverseAsync(
        Guid originalEntryId,
        string description,
        string sourceType,
        Guid? sourceId,
        Guid postedByUserId,
        CancellationToken cancellationToken = default)
    {
        var lines = await _context.JournalLines.AsNoTracking()
            .Where(l => l.JournalEntryId == originalEntryId && !l.IsDeleted)
            .Select(l => new JournalLineSpec(l.Account!.Code, l.Credit, l.Debit, l.Memo))
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
            throw new InvalidOperationException($"Journal entry {originalEntryId} has no lines to reverse.");

        return (await PostAsync(description, sourceType, sourceId, lines, postedByUserId,
            cancellationToken: cancellationToken))!;
    }

    public async Task<string> AllocateReceiptNumberAsync(CancellationToken cancellationToken = default)
    {
        var counter = await CounterAsync(cancellationToken);
        var seq = counter.NextReceiptNumber;
        counter.NextReceiptNumber = seq + 1;
        return $"RC-{seq:D6}";
    }

    private async Task<FinanceCounter> CounterAsync(CancellationToken cancellationToken)
    {
        // A context may post several entries before saving; the tracked counter carries the sequence.
        var counter = _context.FinanceCounters.Local.FirstOrDefault(c => !c.IsDeleted)
            ?? await _context.FinanceCounters.FirstOrDefaultAsync(c => !c.IsDeleted, cancellationToken);

        if (counter is null)
        {
            counter = new FinanceCounter();
            _context.FinanceCounters.Add(counter);
        }

        return counter;
    }

    private async Task<string> AllocateEntryNumberAsync(CancellationToken cancellationToken)
    {
        var counter = await CounterAsync(cancellationToken);
        var seq = counter.NextJournalNumber;
        counter.NextJournalNumber = seq + 1;

        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        return $"JE-{datePart}-{seq:D6}";
    }
}
