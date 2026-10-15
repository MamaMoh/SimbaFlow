using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Finance.Commands;
using SimbaFlow.API.Features.Finance.Queries;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Services;

/// <summary>A wrong payment is taken back with a reversing entry, never deleted; every payment has a receipt.</summary>
public class PaymentVoidTests
{
    private static Task<Application.Common.Models.Result> Void(FinanceTestBed bed, Guid paymentId, string reason = "Bounced") =>
        new VoidPaymentHandler(bed.Db, bed.Journal, bed.User)
            .Handle(new VoidPaymentCommand(paymentId, reason), CancellationToken.None);

    [Fact]
    public async Task Voiding_reverses_the_ledger_and_restores_the_balance()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m));
        var paymentId = await bed.PayAsync(1000m);
        (await bed.ReloadAsync()).Status.Should().Be(CommissionStatus.Settled);

        (await Void(bed, paymentId)).IsSuccess.Should().BeTrue();

        var commission = await bed.ReloadAsync();
        commission.Status.Should().Be(CommissionStatus.Open);
        commission.TotalPaidAmount.Should().Be(0);
        commission.BalanceAmount.Should().Be(1000m);
        (await bed.BalanceAsync(LedgerAccounts.CashBank)).Should().Be(0);
        (await bed.BalanceAsync(LedgerAccounts.Receivable)).Should().Be(1000m);

        var payment = await bed.Db.Payments.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        payment.IsDeleted.Should().BeFalse();
        payment.VoidReason.Should().Be("Bounced");
        payment.VoidJournalEntryId.Should().NotBeNull();
        await bed.LedgerBalancesAsync();
    }

    [Fact]
    public async Task A_payment_cannot_be_voided_twice_or_without_a_reason()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m));
        var paymentId = await bed.PayAsync(400m);

        (await Void(bed, paymentId, " ")).IsSuccess.Should().BeFalse();
        (await Void(bed, paymentId)).IsSuccess.Should().BeTrue();
        (await Void(bed, paymentId)).IsSuccess.Should().BeFalse();
        (await bed.BalanceAsync(LedgerAccounts.CashBank)).Should().Be(0);
    }

    [Fact]
    public async Task After_a_void_the_balance_can_be_paid_again()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m));
        var first = await bed.PayAsync(1000m);
        await Void(bed, first);

        (await bed.TryPayAsync(1000m)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Receipts_are_numbered_in_order_and_show_the_payment()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m));
        var first = await bed.PayAsync(300m);
        var second = await bed.PayAsync(200m);

        var platform = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());
        var handler = new GetPaymentReceiptHandler(bed.Db, platform, bed.User);

        var one = (await handler.Handle(new GetPaymentReceiptQuery(first), CancellationToken.None)).Data!;
        var two = (await handler.Handle(new GetPaymentReceiptQuery(second), CancellationToken.None)).Data!;

        one.ReceiptNumber.Should().Be("RC-000001");
        two.ReceiptNumber.Should().Be("RC-000002");
        two.AmountEtb.Should().Be(200m);
        two.CandidateName.Should().Be("Abebe Kebede");
        two.BalanceEtb.Should().Be(500m);
    }
}
