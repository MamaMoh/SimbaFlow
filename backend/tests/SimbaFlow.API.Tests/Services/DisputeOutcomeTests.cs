using FluentAssertions;
using SimbaFlow.API.Features.Finance.Commands;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>Resolving a dispute can write money off the balance, and the ledger shows it.</summary>
public class DisputeOutcomeTests
{
    private static async Task<Guid> Open(FinanceTestBed bed, decimal? amount = null)
    {
        var result = await new OpenDisputeHandler(bed.Db, bed.User).Handle(
            new OpenDisputeCommand(bed.Commission.Id, "Candidate says medical was not done", amount),
            CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Data;
    }

    private static Task<Result> Resolve(FinanceTestBed bed, Guid id, string outcome, decimal? amount = null) =>
        new ResolveDisputeHandler(bed.Db, bed.Journal, bed.User).Handle(
            new ResolveDisputeCommand(id, "Agreed", outcome, amount), CancellationToken.None);

    [Fact]
    public async Task Waived_writes_off_the_disputed_amount()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m), ("Medical", 200m));
        var dispute = await Open(bed, 200m);

        (await Resolve(bed, dispute, "Waived")).IsSuccess.Should().BeTrue();

        var commission = await bed.ReloadAsync();
        commission.TotalAdjustedAmount.Should().Be(200m);
        commission.BalanceAmount.Should().Be(1000m);
        commission.Status.Should().Be(CommissionStatus.Partial);
        (await bed.BalanceAsync(LedgerAccounts.FeeWaivers)).Should().Be(200m);
        (await bed.BalanceAsync(LedgerAccounts.Receivable)).Should().Be(1000m);
        await bed.LedgerBalancesAsync();
    }

    [Fact]
    public async Task Waiving_with_no_amount_writes_off_the_whole_balance_and_settles()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m));
        await bed.PayAsync(300m);
        var dispute = await Open(bed);

        (await Resolve(bed, dispute, "Waived")).IsSuccess.Should().BeTrue();

        var commission = await bed.ReloadAsync();
        commission.BalanceAmount.Should().Be(0);
        commission.Status.Should().Be(CommissionStatus.Settled);
    }

    [Fact]
    public async Task Adjusted_writes_off_the_agreed_amount_but_not_more_than_owed()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m));
        var dispute = await Open(bed);

        (await Resolve(bed, dispute, "Adjusted", 1500m)).IsSuccess.Should().BeFalse();
        (await Resolve(bed, dispute, "Adjusted")).IsSuccess.Should().BeFalse();
        (await Resolve(bed, dispute, "Adjusted", 250m)).IsSuccess.Should().BeTrue();

        (await bed.ReloadAsync()).BalanceAmount.Should().Be(750m);
    }

    [Fact]
    public async Task Enforced_changes_no_money()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m));
        var dispute = await Open(bed, 400m);

        (await Resolve(bed, dispute, "Enforced")).IsSuccess.Should().BeTrue();

        var commission = await bed.ReloadAsync();
        commission.BalanceAmount.Should().Be(1000m);
        commission.Status.Should().Be(CommissionStatus.Open);
        (await bed.BalanceAsync(LedgerAccounts.FeeWaivers)).Should().Be(0);
    }

    [Fact]
    public async Task Disputed_amount_cannot_exceed_the_balance()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m));

        var result = await new OpenDisputeHandler(bed.Db, bed.User).Handle(
            new OpenDisputeCommand(bed.Commission.Id, "Too much", 5000m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
