using FluentAssertions;
using SimbaFlow.API.Features.Accounting.Commands;
using SimbaFlow.API.Features.Accounting.Queries;
using SimbaFlow.API.Features.Finance.Commands;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>The statements agree with each other and with what was posted.</summary>
public class AccountingReportTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Fees 1,000 agency + 200 medical + 300 partner share, paid 1,000; 100 waived on dispute;
    /// medical paid out (200) and salaries (400).
    /// </summary>
    private static async Task<FinanceTestBed> BusyMonthAsync()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m), ("Medical", 200m), ("PartnerShare", 300m));
        await bed.PayAsync(1000m);

        var dispute = await new OpenDisputeHandler(bed.Db, bed.User).Handle(
            new OpenDisputeCommand(bed.Commission.Id, "Discount agreed", 100m), CancellationToken.None);
        await new ResolveDisputeHandler(bed.Db, bed.Journal, bed.User).Handle(
            new ResolveDisputeCommand(dispute.Data, "OK", "Waived"), CancellationToken.None);

        foreach (var (category, amount) in new[] { ("MedicalPayout", 200m), ("Salaries", 400m) })
        {
            var r = await new RecordExpenseHandler(bed.Db, bed.Fx, bed.Journal, bed.User).Handle(
                new RecordExpenseCommand(Today, category, category, amount, "ETB", "Cash", null, null, null),
                CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error);
        }
        return bed;
    }

    [Fact]
    public async Task Profit_and_loss_counts_only_the_agencys_own_income_and_costs()
    {
        var bed = await BusyMonthAsync();

        var pl = (await new GetProfitAndLossHandler(bed.Db)
            .Handle(new GetProfitAndLossQuery(Today.AddDays(-1), Today), CancellationToken.None)).Data!;

        pl.TotalRevenue.Should().Be(900m, "1,000 agency fee less the 100 waived");
        pl.TotalExpenses.Should().Be(400m, "the medical payout is not a cost");
        pl.NetProfit.Should().Be(500m);
    }

    [Fact]
    public async Task Balance_sheet_balances_with_profit_as_retained_earnings()
    {
        var bed = await BusyMonthAsync();

        var bs = (await new GetBalanceSheetHandler(bed.Db)
            .Handle(new GetBalanceSheetQuery(Today), CancellationToken.None)).Data!;

        bs.TotalAssets.Should().Be(bs.TotalLiabilitiesAndEquity);
        bs.Assets.Single(a => a.Code == LedgerAccounts.CashBank).Amount.Should().Be(400m);
        bs.Assets.Single(a => a.Code == LedgerAccounts.Receivable).Amount.Should().Be(400m);
        bs.Liabilities.Single(l => l.Code == LedgerAccounts.PartnerSharePayable).Amount.Should().Be(300m);
        bs.Liabilities.Should().NotContain(l => l.Code == LedgerAccounts.MedicalPayable);
        bs.RetainedEarnings.Should().Be(500m);
    }

    [Fact]
    public async Task Trial_balance_columns_agree()
    {
        var bed = await BusyMonthAsync();

        var tb = (await new GetTrialBalanceHandler(bed.Db)
            .Handle(new GetTrialBalanceQuery(Today), CancellationToken.None)).Data!;

        tb.TotalDebit.Should().Be(tb.TotalCredit).And.BeGreaterThan(0);
    }

    [Fact]
    public async Task Nothing_before_the_period_counts_and_the_journal_lists_every_entry()
    {
        var bed = await BusyMonthAsync();

        var pl = (await new GetProfitAndLossHandler(bed.Db)
            .Handle(new GetProfitAndLossQuery(Today.AddDays(1), Today.AddDays(5)), CancellationToken.None)).Data!;
        pl.NetProfit.Should().Be(0);

        var journal = (await new GetJournalEntriesHandler(bed.Db)
            .Handle(new GetJournalEntriesQuery(PageSize: 2), CancellationToken.None)).Data!;
        journal.TotalCount.Should().Be(5, "fees, payment, waiver and two expenses");
        journal.Items.Should().HaveCount(2);
    }
}
