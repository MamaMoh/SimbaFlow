using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Accounting.Commands;
using SimbaFlow.API.Features.Accounting.Queries;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>Money paid out comes off cash; payouts settle what was collected for others.</summary>
public class ExpenseTests
{
    private static readonly DateOnly Day = new(2026, 10, 10);

    private static Task<Application.Common.Models.Result<Guid>> Record(
        FinanceTestBed bed, string category, decimal amount, string currency = "ETB") =>
        new RecordExpenseHandler(bed.Db, bed.Fx, bed.Journal, bed.User).Handle(
            new RecordExpenseCommand(Day, category, "Paid", amount, currency, "Cash", "Clinic", null, null),
            CancellationToken.None);

    [Fact]
    public async Task Operating_expense_debits_its_account_and_credits_cash()
    {
        var bed = await FinanceTestBed.CreateAsync();

        (await Record(bed, "Salaries", 8000m)).IsSuccess.Should().BeTrue();

        (await bed.BalanceAsync(LedgerAccounts.Salaries)).Should().Be(8000m);
        (await bed.BalanceAsync(LedgerAccounts.CashBank)).Should().Be(-8000m);
        await bed.LedgerBalancesAsync();
    }

    [Fact]
    public async Task Medical_payout_settles_the_medical_payable_not_profit()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m), ("Medical", 200m));
        await bed.PayAsync(1200m);

        (await Record(bed, "MedicalPayout", 200m)).IsSuccess.Should().BeTrue();

        (await bed.BalanceAsync(LedgerAccounts.MedicalPayable)).Should().Be(0);
        (await bed.BalanceAsync(LedgerAccounts.CashBank)).Should().Be(1000m);
        (await bed.BalanceAsync(LedgerAccounts.CommissionRevenue)).Should().Be(-1000m);
    }

    [Fact]
    public async Task Foreign_currency_expense_is_converted_and_dated()
    {
        var bed = await FinanceTestBed.CreateAsync();
        var id = (await Record(bed, "TransportAndTravel", 10m, "USD")).Data;

        var expense = await bed.Db.Expenses.AsNoTracking().FirstAsync(e => e.Id == id);
        expense.AmountEtb.Should().Be(550m);
        var entry = await bed.Db.JournalEntries.AsNoTracking().FirstAsync(j => j.Id == expense.JournalEntryId);
        DateOnly.FromDateTime(entry.PostedAt).Should().Be(Day);
    }

    [Fact]
    public async Task Voided_expense_is_reversed_and_still_listed()
    {
        var bed = await FinanceTestBed.CreateAsync();
        var id = (await Record(bed, "Marketing", 500m)).Data;

        var voided = await new VoidExpenseHandler(bed.Db, bed.Journal, bed.User)
            .Handle(new VoidExpenseCommand(id, "Duplicate"), CancellationToken.None);
        voided.IsSuccess.Should().BeTrue();

        (await bed.BalanceAsync(LedgerAccounts.Marketing)).Should().Be(0);
        (await bed.BalanceAsync(LedgerAccounts.CashBank)).Should().Be(0);
        var list = (await new GetExpensesHandler(bed.Db).Handle(new GetExpensesQuery(), CancellationToken.None)).Data!;
        list.Single().VoidReason.Should().Be("Duplicate");
    }

    [Fact]
    public async Task Unknown_category_and_zero_amount_are_refused()
    {
        var bed = await FinanceTestBed.CreateAsync();

        (await Record(bed, "Yachts", 10m)).IsSuccess.Should().BeFalse();
        (await Record(bed, "Other", 0m)).IsSuccess.Should().BeFalse();
    }
}
