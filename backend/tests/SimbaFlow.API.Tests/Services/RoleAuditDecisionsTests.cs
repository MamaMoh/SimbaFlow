using System.Reflection;
using FluentAssertions;
using NSubstitute;
using SimbaFlow.API.Features.Arrival.Commands;
using SimbaFlow.API.Features.Tenants;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Infrastructure.Persistence.Seeds;
using SimbaFlow.Infrastructure.Workflow;
using Xunit;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The role decisions taken after the 2026-10-15 permissions audit, pinned so they stay taken.
/// </summary>
public class RoleAuditDecisionsTests
{
    private static readonly Dictionary<string, string[]> Roles = ReadPrivate<Dictionary<string, string[]>>(
        typeof(RolePermissionSeeder), "RolePermissionMap");

    private static readonly HashSet<string> Catalogue =
        ReadPrivate<(string Module, string Code, string Name)[]>(typeof(PermissionSeeder), "Permissions")
            .Select(p => p.Code)
            .ToHashSet();

    private static T ReadPrivate<T>(Type type, string field) =>
        (T)type.GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    // ──── Permissions nothing checked are gone ────

    [Theory]
    [InlineData("auth.login")]
    [InlineData("accounting.reconcile")]
    [InlineData("notification.send")]
    [InlineData("tenant.provision")]
    [InlineData("tenant.manage")]
    public void APermissionNothingChecksIsNotInTheCatalogue(string code) =>
        Catalogue.Should().NotContain(code, "ticking it on a role would change nothing");

    // ──── Commission is entered under commission.create ────

    [Fact]
    public void AddingToCommissionNeedsCommissionCreate() =>
        new AddToCommissionCommand(Guid.NewGuid()).RequiredPermission.Should().Be("commission.create");

    [Theory]
    [InlineData("Commission")]
    [InlineData("commission ")]
    public void MovingIntoCommissionNeedsCommissionCreate(string stage) =>
        WorkflowEngineService.EnteringNeedsPermission(stage).Should().Be("commission.create");

    [Theory]
    [InlineData("Arrival")]
    [InlineData("Embassy")]
    [InlineData(null)]
    public void OtherStagesNeedNothingExtra(string? stage) =>
        WorkflowEngineService.EnteringNeedsPermission(stage).Should().BeNull();

    [Theory]
    [InlineData("AgencyOwner")]
    [InlineData("OfficeManager")]
    [InlineData("FinanceOfficer")]
    public void TheDesksThatOpenCommissionsHoldIt(string role) =>
        Roles[role].Should().Contain("commission.create");

    [Theory]
    [InlineData("FieldAgent")]
    [InlineData("EmbassyOfficer")]
    public void TheFieldDoesNot(string role) =>
        Roles[role].Should().NotContain("commission.create");

    // ──── Subscription billing ────

    private static ICurrentUserService Caller(string role, params string[] permissions)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.Roles.Returns([role]);
        user.HasPermission(Arg.Any<string>()).Returns(c => permissions.Contains(c.Arg<string>()));
        return user;
    }

    [Fact]
    public void TheOwnerSeesWhatIsOwed() =>
        SubscriptionModule.SeesSubscriptionBilling(Caller("AgencyOwner")).Should().BeTrue();

    [Fact]
    public void FinanceSeesWhatIsOwed() =>
        SubscriptionModule.SeesSubscriptionBilling(
            Caller("FinanceOfficer", Roles["FinanceOfficer"])).Should().BeTrue();

    [Theory]
    [InlineData("Auditor")]
    [InlineData("OfficeManager")]
    [InlineData("DataEntryClerk")]
    [InlineData("FieldAgent")]
    public void OtherStaffDoNot(string role) =>
        SubscriptionModule.SeesSubscriptionBilling(Caller(role, Roles[role])).Should().BeFalse();

    // ──── The seeder follows the map exactly ────

    [Fact]
    public void ReconcileAddsWhatIsMissingAndRemovesWhatIsNoLongerListed()
    {
        Guid kept = Guid.NewGuid(), added = Guid.NewGuid(), dropped = Guid.NewGuid();

        var (toAdd, toRemove) = RolePermissionSeeder.Reconcile(
            new HashSet<Guid> { kept, added }, [kept, dropped]);

        toAdd.Should().BeEquivalentTo([added]);
        toRemove.Should().BeEquivalentTo([dropped]);
    }

    [Fact]
    public void ReconcileLeavesARoleThatAlreadyMatchesAlone()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();

        var (toAdd, toRemove) = RolePermissionSeeder.Reconcile(new HashSet<Guid> { a, b }, [a, b]);

        toAdd.Should().BeEmpty();
        toRemove.Should().BeEmpty();
    }
}
