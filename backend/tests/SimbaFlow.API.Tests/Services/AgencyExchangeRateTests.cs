using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Accounting.Commands;
using SimbaFlow.API.Features.Accounting.Queries;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Each agency keeps its own exchange rates; the platform's are only the fallback.
/// </summary>
public class AgencyExchangeRateTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);
    private readonly string _db = Guid.NewGuid().ToString();
    private readonly Guid _agencyA = Guid.NewGuid();
    private readonly Guid _agencyB = Guid.NewGuid();

    private PlatformDbContext Platform(ICurrentUserService user) =>
        new(new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase(_db).Options, user);

    private static ICurrentUserService UserOf(Guid? tenantId, bool superAdmin = false)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid().ToString());
        user.TenantId.Returns(tenantId);
        user.IsSuperAdmin.Returns(superAdmin);
        return user;
    }

    private async Task<decimal> RateFor(Guid? tenantId, DateOnly asOf)
    {
        var user = UserOf(tenantId);
        return await new ExchangeRateService(Platform(user), user).ResolveRateToEtbAsync("USD", asOf);
    }

    private async Task Save(ICurrentUserService user, decimal rate, DateOnly date)
    {
        var result = await new UpsertExchangeRateHandler(Platform(user), user)
            .Handle(new UpsertExchangeRateCommand("usd", "etb", rate, date), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task One_agencys_rate_does_not_change_another_agencys_conversions()
    {
        await Save(UserOf(null, superAdmin: true), 120m, Day);
        await Save(UserOf(_agencyA), 150m, Day);

        (await RateFor(_agencyA, Day)).Should().Be(150m);
        (await RateFor(_agencyB, Day)).Should().Be(120m);
    }

    [Fact]
    public async Task Agency_falls_back_to_platform_default_when_it_has_no_rate_of_its_own()
    {
        await Save(UserOf(null, superAdmin: true), 120m, Day);

        (await RateFor(_agencyA, Day.AddDays(5))).Should().Be(120m);
    }

    [Fact]
    public async Task Agency_rate_applies_only_from_its_effective_date()
    {
        await Save(UserOf(null, superAdmin: true), 120m, Day);
        await Save(UserOf(_agencyA), 150m, Day.AddDays(10));

        (await RateFor(_agencyA, Day.AddDays(2))).Should().Be(120m);
        (await RateFor(_agencyA, Day.AddDays(10))).Should().Be(150m);
    }

    [Fact]
    public async Task Missing_rate_everywhere_is_an_error()
    {
        await Save(UserOf(_agencyB), 150m, Day);

        var act = () => RateFor(_agencyA, Day);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Saving_the_same_pair_and_date_updates_only_that_agencys_row()
    {
        await Save(UserOf(_agencyA), 150m, Day);
        await Save(UserOf(_agencyB), 140m, Day);
        await Save(UserOf(_agencyA), 155m, Day);

        await using var db = Platform(UserOf(null));
        var rows = await db.ExchangeRates.ToListAsync();
        rows.Should().HaveCount(2);
        rows.Single(r => r.TenantId == _agencyA).Rate.Should().Be(155m);
        rows.Single(r => r.TenantId == _agencyB).Rate.Should().Be(140m);
    }

    [Fact]
    public async Task Account_without_an_agency_or_platform_role_cannot_set_defaults()
    {
        var user = UserOf(null);
        var result = await new UpsertExchangeRateHandler(Platform(user), user)
            .Handle(new UpsertExchangeRateCommand("USD", "ETB", 1m, Day), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Listing_shows_own_rates_and_defaults_but_never_another_agencys()
    {
        await Save(UserOf(null, superAdmin: true), 120m, Day);
        await Save(UserOf(_agencyA), 150m, Day);
        await Save(UserOf(_agencyB), 140m, Day);

        var user = UserOf(_agencyA);
        var result = await new GetExchangeRatesHandler(Platform(user), user)
            .Handle(new GetExchangeRatesQuery(), CancellationToken.None);

        result.Data!.Select(r => (r.Rate, r.IsAgencyRate))
            .Should().BeEquivalentTo(new[] { (150m, true), (120m, false) });
    }
}
