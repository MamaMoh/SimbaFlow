using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using SimbaFlow.API.Features.Tenants;
using SimbaFlow.API.Features.Users;
using SimbaFlow.Application.Common.Behaviors;
using SimbaFlow.Application.Common.Exceptions;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Options;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The tools the platform team uses to run and help agencies: the user limit, renewal reminders
/// and suspension for non-payment, and the support desk's role.
/// </summary>
public class PlatformSupportToolsTests : IDisposable
{
    private readonly PlatformDbContext _db;

    public PlatformSupportToolsTests()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new PlatformDbContext(options, Substitute.For<ICurrentUserService>());
    }

    public void Dispose() => _db.Dispose();

    private async Task<TenantInfo> GivenAgency(Action<TenantInfo>? shape = null)
    {
        var id = Guid.NewGuid();
        var tenant = new TenantInfo
        {
            Id = id,
            Name = "Tango Agency",
            Slug = $"tango-{id:N}",
            SchemaName = $"tenant_{id:N}",
            ContactEmail = "office@tango.test",
        };
        shape?.Invoke(tenant);
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();
        return tenant;
    }

    private async Task GivenUsers(Guid tenantId, int active, int disabled = 0)
    {
        for (var i = 0; i < active + disabled; i++)
            _db.Users.Add(new ApplicationUser
            {
                Id = Guid.NewGuid(), TenantId = tenantId, IsActive = i < active,
                UserName = $"u{Guid.NewGuid():N}", Email = $"u{i}@tango.test",
            });
        await _db.SaveChangesAsync();
    }

    // ──── User limit ────

    [Fact]
    public async Task AnAgencyBelowItsLimitMayAddSomeone()
    {
        var agency = await GivenAgency(t => t.MaxUsers = 3);
        await GivenUsers(agency.Id, active: 2);

        (await SeatLimit.RefusalForOneMoreAsync(_db, agency.Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task AnAgencyAtItsLimitIsRefused()
    {
        var agency = await GivenAgency(t => t.MaxUsers = 2);
        await GivenUsers(agency.Id, active: 2);

        (await SeatLimit.RefusalForOneMoreAsync(_db, agency.Id, default))
            .Should().Contain("limit of 2");
    }

    [Fact]
    public async Task DisabledAccountsDoNotTakeASeat()
    {
        var agency = await GivenAgency(t => t.MaxUsers = 2);
        await GivenUsers(agency.Id, active: 1, disabled: 5);

        (await SeatLimit.RefusalForOneMoreAsync(_db, agency.Id, default)).Should().BeNull();
    }

    // ──── Renewal rules ────

    private static readonly DateOnly Today = new(2026, 10, 15);

    [Theory]
    [InlineData(10, null)]
    [InlineData(5, RenewalReminder.DueSoon)]
    [InlineData(0, RenewalReminder.DueToday)]
    [InlineData(-1, RenewalReminder.Overdue)]
    public void TheReminderFollowsTheDueDate(int daysAway, RenewalReminder? expected)
    {
        SubscriptionRules.ReminderFor(Today.AddDays(daysAway), BillingCycle.Monthly, Today)
            .Should().Be(expected);
    }

    [Fact]
    public void AnAgencyIsSuspendedOnlyAfterTheGracePeriod()
    {
        SubscriptionRules.ShouldAutoSuspend(TenantStatus.Active, 100, Today.AddDays(-13), Today, 14).Should().BeFalse();
        SubscriptionRules.ShouldAutoSuspend(TenantStatus.Active, 100, Today.AddDays(-14), Today, 14).Should().BeTrue();
    }

    [Fact]
    public void AFreeOrSwitchedOffAgencyIsNeverSuspended()
    {
        SubscriptionRules.ShouldAutoSuspend(TenantStatus.Active, 0, Today.AddDays(-60), Today, 14).Should().BeFalse();
        SubscriptionRules.ShouldAutoSuspend(TenantStatus.Active, 100, Today.AddDays(-60), Today, 0).Should().BeFalse();
    }

    // ──── The reminder pass ────

    private (IServiceProvider Services, IEmailService Email) Services(int graceDays = 14)
    {
        var email = Substitute.For<IEmailService>();
        email.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var options = Substitute.For<IOptionsMonitor<EmailOptions>>();
        options.CurrentValue.Returns(new EmailOptions { AppBaseUrl = "https://app.test" });
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Billing:AutoSuspendAfterDays"] = graceDays.ToString() })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IPlatformDbContext>(_db)
            .AddSingleton(email)
            .AddSingleton(Substitute.For<ITenantSchemaResolver>())
            .AddSingleton(Substitute.For<IAuditService>())
            .AddSingleton<IConfiguration>(config)
            .AddSingleton(options)
            .BuildServiceProvider();
        return (services, email);
    }

    [Fact]
    public async Task ADueSoonReminderGoesOutOnce()
    {
        var agency = await GivenAgency(t =>
        {
            t.SubscriptionAmount = 1000;
            t.NextPaymentDue = Today.AddDays(3);
        });
        var (services, email) = Services();

        await BillingReminderService.RunOnceAsync(services, Today, default);
        await BillingReminderService.RunOnceAsync(services, Today, default);

        await email.Received(1).SendAsync("office@tango.test", Arg.Is<string>(s => s.Contains("due soon")),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await _db.Tenants.FindAsync(agency.Id))!.LastRenewalReminder.Should().Be(RenewalReminder.DueSoon);
    }

    [Fact]
    public async Task ALaterStageIsSentEvenAfterAnEarlierOne()
    {
        await GivenAgency(t =>
        {
            t.SubscriptionAmount = 1000;
            t.NextPaymentDue = Today.AddDays(-2);
            t.LastRenewalReminder = RenewalReminder.DueSoon;
            t.LastRenewalReminderFor = Today.AddDays(-2);
        });
        var (services, email) = Services();

        await BillingReminderService.RunOnceAsync(services, Today, default);

        await email.Received(1).SendAsync(Arg.Any<string>(), Arg.Is<string>(s => s.Contains("overdue")),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnAgencyUnpaidPastTheGracePeriodIsSuspended()
    {
        var agency = await GivenAgency(t =>
        {
            t.SubscriptionAmount = 1000;
            t.NextPaymentDue = Today.AddDays(-20);
        });
        var (services, email) = Services(graceDays: 14);

        await BillingReminderService.RunOnceAsync(services, Today, default);

        var after = (await _db.Tenants.FindAsync(agency.Id))!;
        after.SubscriptionStatus.Should().Be(TenantStatus.Suspended);
        after.AutoSuspendedAt.Should().NotBeNull();
        await email.Received(1).SendAsync(Arg.Any<string>(), Arg.Is<string>(s => s.Contains("suspended")),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithAutoSuspendSwitchedOffAnOverdueAgencyKeepsWorking()
    {
        var agency = await GivenAgency(t =>
        {
            t.SubscriptionAmount = 1000;
            t.NextPaymentDue = Today.AddDays(-60);
        });
        var (services, _) = Services(graceDays: 0);

        await BillingReminderService.RunOnceAsync(services, Today, default);

        (await _db.Tenants.FindAsync(agency.Id))!.SubscriptionStatus.Should().Be(TenantStatus.Active);
    }

    // ──── The support desk ────

    private static ICurrentUserService Caller(Guid? tenantId, params string[] roles)
    {
        var svc = Substitute.For<ICurrentUserService>();
        svc.UserId.Returns(Guid.NewGuid().ToString());
        svc.TenantId.Returns(tenantId);
        svc.Roles.Returns(roles);
        return svc;
    }

    private static ApplicationUser User(Guid? tenantId, bool superAdmin = false) =>
        new() { Id = Guid.NewGuid(), TenantId = tenantId, IsSuperAdmin = superAdmin };

    [Fact]
    public void SupportCanHelpAgencyStaffButNotPlatformAccounts()
    {
        var support = Caller(null, PlatformRoles.Support);
        UserAccessGuard.CanManage(support, User(Guid.NewGuid())).Should().BeTrue();
        UserAccessGuard.CanManage(support, User(null)).Should().BeFalse();
        UserAccessGuard.CanManage(support, User(null, superAdmin: true)).Should().BeFalse();
    }

    [Fact]
    public void SupportIsGivenOnlyByThePlatformTeam()
    {
        var owner = Caller(Guid.NewGuid(), AgencyRoles.Owner);
        UserAccessGuard.CanGrantRole(owner, PlatformRoles.Support).Should().BeFalse();
        UserAccessGuard.IsPlatformRole(PlatformRoles.Support).Should().BeTrue();
    }

    [Fact]
    public void SupportDoesNotCarryPlatformAuthority()
    {
        PlatformRoles.Includes(PlatformRoles.Support).Should().BeFalse();
        PlatformRoles.IsStaff([PlatformRoles.Support]).Should().BeTrue();
    }

    private record StaffOnly : IRequest<Result>, IRequirePlatformStaff;

    [Fact]
    public async Task StaffWorkAdmitsSupportAndRefusesAnAgencyOwner()
    {
        RequestHandlerDelegate<Result> next = _ => Task.FromResult(Result.Success());

        var asSupport = new AuthorizationBehavior<StaffOnly, Result>(Caller(null, PlatformRoles.Support));
        (await asSupport.Handle(new StaffOnly(), next, default)).IsSuccess.Should().BeTrue();

        var asOwner = new AuthorizationBehavior<StaffOnly, Result>(Caller(Guid.NewGuid(), AgencyRoles.Owner));
        var act = () => asOwner.Handle(new StaffOnly(), next, default);
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
