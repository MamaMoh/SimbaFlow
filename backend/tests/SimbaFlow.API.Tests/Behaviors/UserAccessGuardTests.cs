using FluentAssertions;
using NSubstitute;
using SimbaFlow.API.Features.Users;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Regression tests for the SaaS tenant/privilege boundary on user management.
/// A tenant admin must never be able to view or modify users outside their own
/// tenant, nor touch a platform SuperAdmin. A SuperAdmin may manage anyone.
/// </summary>
public class UserAccessGuardTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static ICurrentUserService Caller(bool superAdmin, Guid? tenantId, params string[] roles)
    {
        var svc = Substitute.For<ICurrentUserService>();
        svc.IsSuperAdmin.Returns(superAdmin);
        svc.TenantId.Returns(tenantId);
        svc.Roles.Returns(roles);
        return svc;
    }

    private static ICurrentUserService PlatformAdmin() =>
        Caller(superAdmin: false, tenantId: null, "PlatformAdmin");

    private static ApplicationUser User(Guid? tenantId, bool superAdmin = false) =>
        new() { Id = Guid.NewGuid(), TenantId = tenantId, IsSuperAdmin = superAdmin };

    [Fact]
    public void SuperAdmin_CanManage_AnyUser()
    {
        var caller = Caller(superAdmin: true, tenantId: null);
        UserAccessGuard.CanManage(caller, User(TenantA)).Should().BeTrue();
        UserAccessGuard.CanManage(caller, User(TenantB, superAdmin: true)).Should().BeTrue();
        UserAccessGuard.CanManage(caller, User(null)).Should().BeTrue();
    }

    [Fact]
    public void TenantAdmin_CanManage_SameTenantUser()
    {
        var caller = Caller(superAdmin: false, tenantId: TenantA);
        UserAccessGuard.CanManage(caller, User(TenantA)).Should().BeTrue();
    }

    [Fact]
    public void TenantAdmin_CannotManage_OtherTenantUser()
    {
        var caller = Caller(superAdmin: false, tenantId: TenantA);
        UserAccessGuard.CanManage(caller, User(TenantB)).Should().BeFalse();
    }

    [Fact]
    public void TenantAdmin_CannotManage_PlatformSuperAdmin_EvenSameTenant()
    {
        var caller = Caller(superAdmin: false, tenantId: TenantA);
        UserAccessGuard.CanManage(caller, User(TenantA, superAdmin: true)).Should().BeFalse();
    }

    [Fact]
    public void TenantAdmin_CannotManage_TenantlessUser()
    {
        var caller = Caller(superAdmin: false, tenantId: TenantA);
        UserAccessGuard.CanManage(caller, User(null)).Should().BeFalse();
    }

    [Fact]
    public void TenantlessNonSuperAdmin_CannotManage_Anyone()
    {
        var caller = Caller(superAdmin: false, tenantId: null);
        UserAccessGuard.CanManage(caller, User(TenantA)).Should().BeFalse();
    }

    // ──── Granting roles ────
    //
    // Whether a caller may touch a user and whether they may hand out a given role are separate
    // questions. Only the first was being asked, and AgencyOwner ships with users.write — so every
    // customer's own owner was one request away from the platform.

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("superadmin")]
    [InlineData("PlatformAdmin")]
    public void TenantAdmin_CannotGrant_APlatformRole(string role)
    {
        var caller = Caller(superAdmin: false, tenantId: TenantA);
        UserAccessGuard.CanGrantRole(caller, role).Should().BeFalse(
            "an agency owner who can name themselves {0} owns the platform, not their agency", role);
    }

    [Theory]
    [InlineData("OfficeManager")]
    [InlineData("DataEntryClerk")]
    public void TenantAdmin_CanGrant_AnOrdinaryRole(string role)
    {
        var caller = Caller(superAdmin: false, tenantId: TenantA);
        UserAccessGuard.CanGrantRole(caller, role).Should().BeTrue();
    }

    [Fact]
    public void SuperAdmin_CanGrant_AnyRole()
    {
        var caller = Caller(superAdmin: true, tenantId: null);
        UserAccessGuard.CanGrantRole(caller, "SuperAdmin").Should().BeTrue();
        UserAccessGuard.CanGrantRole(caller, "PlatformAdmin").Should().BeTrue();
    }

    [Fact]
    public void TheOffendingRolesAreNamed_SoTheRefusalCanSayWhy()
    {
        var caller = Caller(superAdmin: false, tenantId: TenantA);

        UserAccessGuard.RolesCallerMayNotGrant(
                caller, ["OfficeManager", "SuperAdmin", "Auditor", "PlatformAdmin"])
            .Should().BeEquivalentTo(["SuperAdmin", "PlatformAdmin"]);
    }

    [Fact]
    public void NothingIsRefused_WhenEveryRoleIsOrdinary()
    {
        var caller = Caller(superAdmin: false, tenantId: TenantA);
        UserAccessGuard.RolesCallerMayNotGrant(caller, ["OfficeManager", "Auditor"])
            .Should().BeEmpty();
    }

    // ──── Platform administrators ────
    //
    // Seeded with no tenant on purpose, which made the tenant comparison fail for every target:
    // the role passed the permission check and was then refused on every user in the system.

    [Fact]
    public void PlatformAdmin_CanManage_AUserInAnyTenant()
    {
        UserAccessGuard.CanManage(PlatformAdmin(), User(TenantA)).Should().BeTrue();
        UserAccessGuard.CanManage(PlatformAdmin(), User(TenantB)).Should().BeTrue();
    }

    [Fact]
    public void PlatformAdmin_CannotManage_ASuperAdmin()
    {
        UserAccessGuard.CanManage(PlatformAdmin(), User(null, superAdmin: true)).Should().BeFalse(
            "that is the line between administering the platform and owning it");
    }

    [Fact]
    public void PlatformAdmin_CannotGrant_APlatformRole()
    {
        UserAccessGuard.CanGrantRole(PlatformAdmin(), "SuperAdmin").Should().BeFalse();
    }

    [Fact]
    public void ATokenMissingItsTenant_IsNotTreatedAsAPlatformAdmin()
    {
        // The distinction is the role, not the absence of a tenant. Keyed on "no tenant and holds
        // users.write" instead, a truncated or malformed token would be read as platform-wide
        // access rather than as no access.
        var caller = Caller(superAdmin: false, tenantId: null, "AgencyOwner");
        UserAccessGuard.CanManage(caller, User(TenantA)).Should().BeFalse();
    }

    // ──── Who may exist without an agency ────

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("PlatformAdmin")]
    [InlineData("platformadmin")]
    public void PlatformRolesAreRecognised(string role) =>
        UserAccessGuard.IsPlatformRole(role).Should().BeTrue();

    [Theory]
    [InlineData("DataEntryClerk")]
    [InlineData("AgencyOwner")]
    [InlineData("FieldAgent")]
    public void AnAgencyRoleIsNotAPlatformRole(string role) =>
        UserAccessGuard.IsPlatformRole(role).Should().BeFalse(
            "an account with this role and no agency resolves to no schema — it signs in and finds "
            + "every page empty, which is a broken account rather than a limited one");
}
