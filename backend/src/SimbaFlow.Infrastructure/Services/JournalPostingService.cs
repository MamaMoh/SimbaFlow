using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Finance;

namespace SimbaFlow.Infrastructure.Services;

public sealed class JournalPostingService : IJournalPostingService
{
    public const string SourceTypeCommissionPayment = "CommissionPayment";

    private readonly ITenantDbContext _context;
    private readonly ILogger<JournalPostingService> _logger;

    public JournalPostingService(ITenantDbContext context, ILogger<JournalPostingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<JournalEntry> PostCommissionPaymentAsync(
        Payment payment,
        Commission commission,
        Guid postedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (payment.AmountEtb <= 0)
            throw new InvalidOperationException("Payment AmountEtb must be positive to post a journal.");

        var cash = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Code == FinanceSeedService.CashBankCode && !a.IsDeleted && a.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"System account {FinanceSeedService.CashBankCode} (Cash/Bank) is missing.");

        var revenue = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Code == FinanceSeedService.CommissionRevenueCode && !a.IsDeleted && a.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"System account {FinanceSeedService.CommissionRevenueCode} (Commission Revenue) is missing.");

        var amount = Math.Round(payment.AmountEtb, 2, MidpointRounding.AwayFromZero);
        var entryNumber = await AllocateEntryNumberAsync(cancellationToken);

        var entry = new JournalEntry
        {
            EntryNumber = entryNumber,
            PostedAt = DateTime.UtcNow,
            Description = $"Commission payment {commission.Id}",
            SourceType = SourceTypeCommissionPayment,
            SourceId = payment.Id,
            PostedByUserId = postedByUserId,
            Lines =
            [
                new JournalLine
                {
                    AccountId = cash.Id,
                    Debit = amount,
                    Credit = 0,
                    Memo = $"Payment {payment.Id}"
                },
                new JournalLine
                {
                    AccountId = revenue.Id,
                    Debit = 0,
                    Credit = amount,
                    Memo = $"Payment {payment.Id}"
                }
            ]
        };

        var debitSum = entry.Lines.Sum(l => l.Debit);
        var creditSum = entry.Lines.Sum(l => l.Credit);
        if (debitSum != creditSum)
            throw new InvalidOperationException(
                $"Unbalanced journal: debit {debitSum} != credit {creditSum}.");

        _context.JournalEntries.Add(entry);
        payment.JournalEntryId = entry.Id;

        _logger.LogInformation(
            "Posted journal {EntryNumber} for commission {CommissionId} payment {PaymentId} AmountEtb {AmountEtb}",
            entry.EntryNumber,
            commission.Id,
            payment.Id,
            amount);

        return entry;
    }

    private async Task<string> AllocateEntryNumberAsync(CancellationToken cancellationToken)
    {
        var counter = await _context.FinanceCounters
            .FirstOrDefaultAsync(c => !c.IsDeleted, cancellationToken);

        if (counter is null)
        {
            counter = new FinanceCounter { NextJournalNumber = 1 };
            _context.FinanceCounters.Add(counter);
        }

        var seq = counter.NextJournalNumber;
        counter.NextJournalNumber = seq + 1;

        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        return $"JE-{datePart}-{seq:D6}";
    }
}
