using FluentAssertions;
using SimbaFlow.API.Features.Finance.Commands;

namespace SimbaFlow.API.Tests.Services;

/// <summary>A payment may settle the balance but not go past it.</summary>
public class OverpaymentTests
{
    [Fact]
    public async Task Payment_above_the_balance_is_refused_and_posts_nothing()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m));
        await bed.PayAsync(600m);

        var result = await bed.TryPayAsync(500m);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Error.Should().Contain("400.00");
        (await bed.ReloadAsync()).BalanceAmount.Should().Be(400m);
    }

    [Fact]
    public async Task Paying_exactly_the_balance_settles_it()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 1000m));

        await bed.PayAsync(1000m);

        (await bed.ReloadAsync()).Status.Should().Be(Domain.Enums.CommissionStatus.Settled);
    }

    [Fact]
    public async Task Foreign_currency_rounding_just_over_the_balance_is_allowed()
    {
        var bed = await FinanceTestBed.CreateAsync();
        await bed.SetFeesAsync(("AgencyFee", 5499.50m));

        // 100 USD at 55 is 5,500 ETB: 50 cents over, inside the tolerance.
        (await bed.TryPayAsync(100m, "USD")).IsSuccess.Should().BeTrue();
        RecordPaymentHandler.OverpaymentTolerance.Should().Be(1m);
    }
}
