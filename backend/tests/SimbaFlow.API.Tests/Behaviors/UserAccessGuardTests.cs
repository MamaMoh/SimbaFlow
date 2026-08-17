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

    private static ICurrentUserService Caller(bool superAdmin, Guid? tenantId)
    {
        var svc = Substitute.For<ICurrentUserService>();
        svc.IsSuperAdmin.Returns(superAdmin);
        svc.TenantId.Returns(tenantId);
        return svc;
    }

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
}
