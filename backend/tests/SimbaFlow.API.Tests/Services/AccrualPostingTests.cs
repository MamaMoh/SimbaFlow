using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Fees are charged to the receivable and split between revenue and money owed onward;
/// payments settle the receivable. Only the agency's own fee is income.
/// </summary>
public class AccrualPostingTests
{
    [Fact]
    public async Task Fees_debit_receivable_and_credit_revenue_or_pass_through()
    {
        var bed = await FinanceTestBed.CreateAsync();

        await bed.SetFeesAsync(("AgencyFee", 1000m), ("PartnerShare", 400m), ("Medical", 200m), ("Ticket", 300m));

        (await bed.BalanceAsync(LedgerAccounts.Receivable)).Should().Be(1900m);
        (await bed.BalanceAsync(LedgerAccounts.CommissionRevenue)).Should().Be(-1000m);
        (await bed.BalanceAsync(LedgerAccounts.PartnerSharePayable)).Should().Be(-400m);
        (await bed.BalanceAsync(LedgerAccounts.MedicalPayable)).Should().Be(-200m);
        (await bed.BalanceAsync(LedgerAccounts.TicketPayable)).Should().Be(-300m);
        await bed.LedgerBalancesAsync();
    }

    [Fact]
    public async Task Payment_moves_receivable_to_cash_and_books_no_revenue()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m), ("Medical", 200m));

        await bed.PayAsync(700m);

        (await bed.BalanceAsync(LedgerAccounts.CashBank)).Should().Be(700m);
        (await bed.BalanceAsync(LedgerAccounts.Receivable)).Should().Be(500m);
        (await bed.BalanceAsync(LedgerAccounts.CommissionRevenue)).Should().Be(-1000m);
        await bed.LedgerBalancesAsync();
    }

    [Fact]
    public async Task Changing_fees_posts_only_the_difference()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m), ("Ticket", 300m));
        await bed.SetFeesAsync(("AgencyFee", 1200m));

        (await bed.BalanceAsync(LedgerAccounts.Receivable)).Should().Be(1200m);
        (await bed.BalanceAsync(LedgerAccounts.CommissionRevenue)).Should().Be(-1200m);
        (await bed.BalanceAsync(LedgerAccounts.TicketPayable)).Should().Be(0m);
        await bed.LedgerBalancesAsync();

        var entries = await bed.Db.JournalEntries.CountAsync();
        await bed.SetFeesAsync(("AgencyFee", 1200m));
        (await bed.Db.JournalEntries.CountAsync()).Should().Be(entries, "nothing changed, so nothing is posted");
    }

    [Fact]
    public async Task Backfill_moves_legacy_revenue_payments_to_receivable_once()
    {
        var bed = await FinanceTestBed.CreateAsync();
        bed.Db.CommissionFees.Add(new CommissionFee
        {
            CommissionId = bed.Commission.Id,
            FeeType = Domain.Enums.FeeType.AgencyFee,
            Amount = 1000m,
            AmountEtb = 1000m,
        });
        var payment = new Payment { CommissionId = bed.Commission.Id, Amount = 400m, AmountEtb = 400m, PaidAt = DateTime.UtcNow };
        bed.Db.Payments.Add(payment);
        await bed.Db.SaveChangesAsync();
        // How payments used to post: Cash Dr / Revenue Cr, and no fee entry at all.
        await bed.Journal.PostAsync("legacy", JournalPostingService.SourceTypeCommissionPayment, payment.Id,
        [
            new(LedgerAccounts.CashBank, 400m, 0),
            new(LedgerAccounts.CommissionRevenue, 0, 400m),
        ], bed.UserId);
        await bed.Db.SaveChangesAsync();

        var seed = new FinanceSeedService(NullLogger<FinanceSeedService>.Instance);
        await seed.EnsureUnit5BackfillAsync(bed.Db);
        await seed.EnsureUnit5BackfillAsync(bed.Db);

        (await bed.BalanceAsync(LedgerAccounts.CashBank)).Should().Be(400m);
        (await bed.BalanceAsync(LedgerAccounts.Receivable)).Should().Be(600m);
        (await bed.BalanceAsync(LedgerAccounts.CommissionRevenue)).Should().Be(-1000m);
        await bed.LedgerBalancesAsync();
    }
}
