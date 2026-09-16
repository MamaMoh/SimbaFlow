using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Auth;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// An agency that has been suspended should stop its people at the door.
///
/// Before this, a suspended agency's staff signed in perfectly well and then met a refusal on every
/// page they opened — which reads as "my account is broken", not as "our subscription needs
/// paying". The distinction matters because only one of those tells them who to call.
/// </summary>
public class TenantSignInGuardTests : IDisposable
{
    private readonly PlatformDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();

    public TenantSignInGuardTests()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new PlatformDbContext(options, Substitute.For<ICurrentUserService>());
    }

    private async Task<Guid> GivenAgency(TenantStatus status, bool deleted = false)
    {
        var id = Guid.NewGuid();
        _context.Tenants.Add(new TenantInfo
        {
            Id = id,
            Name = "Tango Foreign Employment Agent",
            Slug = $"tango-{id:N}",
            SchemaName = $"tenant_{id:N}",
            SubscriptionStatus = status,
            IsDeleted = deleted,
        });
        await _context.SaveChangesAsync();
        return id;
    }

    private static ApplicationUser Staff(Guid? tenantId, bool superAdmin = false) =>
        new() { Id = Guid.NewGuid(), TenantId = tenantId, IsSuperAdmin = superAdmin };

    [Fact]
    public async Task AnActiveAgencysStaffSignIn()
    {
        var agency = await GivenAgency(TenantStatus.Active);

        (await TenantSignInGuard.RefusalFor(Staff(agency), _context, default))
            .Should().BeNull();
    }

    [Fact]
    public async Task ASuspendedAgencysStaffAreRefused_AndToldWhy()
    {
        var agency = await GivenAgency(TenantStatus.Suspended);

        var refusal = await TenantSignInGuard.RefusalFor(Staff(agency), _context, default);

        refusal.Should().NotBeNull();
        refusal.Should().Contain("suspended");
        refusal.Should().Contain("administrator", "the message has to say who can fix it");
        refusal.Should().NotContain("password", "this is not a credentials problem and must not read like one");
    }

    [Fact]
    public async Task ADeactivatedAgencysStaffAreRefused()
    {
        var agency = await GivenAgency(TenantStatus.Deactivated);

        (await TenantSignInGuard.RefusalFor(Staff(agency), _context, default))
            .Should().Contain("deactivated");
    }

    [Fact]
    public async Task AnAgencyThatHasBeenRemovedRefusesToo()
    {
        var agency = await GivenAgency(TenantStatus.Active, deleted: true);

        (await TenantSignInGuard.RefusalFor(Staff(agency), _context, default))
            .Should().NotBeNull("a deleted agency is as unusable as a suspended one");
    }

    [Fact]
    public async Task AnAgencyThatWasNeverThereRefusesToo()
    {
        (await TenantSignInGuard.RefusalFor(Staff(Guid.NewGuid()), _context, default))
            .Should().NotBeNull();
    }

    // ──── Who is exempt ────

    [Fact]
    public async Task APlatformAdminSignsIn_HavingNoAgencyToBeSuspended()
    {
        (await TenantSignInGuard.RefusalFor(Staff(tenantId: null), _context, default))
            .Should().BeNull();
    }

    [Fact]
    public async Task ASuperAdminSignsIn_EvenWhenAttachedToASuspendedAgency()
    {
        var agency = await GivenAgency(TenantStatus.Suspended);

        (await TenantSignInGuard.RefusalFor(Staff(agency, superAdmin: true), _context, default))
            .Should().BeNull(
                "locking out the people who lift suspensions would leave nobody able to lift one");
    }

    [Fact]
    public async Task ReactivatingLetsThemStraightBackIn()
    {
        var agency = await GivenAgency(TenantStatus.Suspended);
        var user = Staff(agency);

        (await TenantSignInGuard.RefusalFor(user, _context, default)).Should().NotBeNull();

        var tenant = await _context.Tenants.FirstAsync(t => t.Id == agency);
        tenant.SubscriptionStatus = TenantStatus.Active;
        await _context.SaveChangesAsync();

        (await TenantSignInGuard.RefusalFor(user, _context, default)).Should().BeNull(
            "the refusal must not outlive the suspension — no cache, no stale read");
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
