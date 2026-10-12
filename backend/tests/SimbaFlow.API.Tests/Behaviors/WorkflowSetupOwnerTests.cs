using FluentAssertions;
using MediatR;
using NSubstitute;
using SimbaFlow.API.Features.Workflow.Commands;
using SimbaFlow.Application.Common.Behaviors;
using SimbaFlow.Application.Common.Exceptions;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The pipeline is the agency's own, and the agency's owner shapes it.
///
/// Every candidate the agency handles walks through these stages, and the rules attached to them
/// are what stops somebody reaching the embassy without a medical. A coordinator who can move a
/// candidate into a stage has no business deciding what that stage requires, and `workflow.configure`
/// was a permission — grantable from inside the agency, which made "only the owner" true only
/// until somebody granted it.
/// </summary>
public class WorkflowSetupOwnerTests
{
    private static AuthorizationBehavior<TRequest, bool> Behavior<TRequest>(
        bool isSuperAdmin, string[] roles, params string[] permissions)
        where TRequest : notnull
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns("user-1");
        user.IsSuperAdmin.Returns(isSuperAdmin);
        user.Roles.Returns(roles);
        user.Permissions.Returns(permissions);
        user.HasPermission(Arg.Any<string>())
            .Returns(call => isSuperAdmin || permissions.Contains(call.Arg<string>()));
        return new AuthorizationBehavior<TRequest, bool>(user);
    }

    private static RequestHandlerDelegate<bool> Ran(Box ran) =>
        _ => { ran.Value = true; return Task.FromResult(true); };

    private sealed class Box { public bool Value; }

    private static CreateStageCommand AStage() => new("Screening", null, 10, 0);

    [Fact]
    public async Task HoldingTheOldPermissionIsNoLongerEnough()
    {
        // This is the whole point of the change: workflow.configure is grantable by whoever
        // administers the agency, so it was never a gate on the agency's own structure.
        var ran = new Box();
        var behavior = Behavior<CreateStageCommand>(
            false, ["OfficeManager"], "workflow.configure", "workflow.view");

        var refuse = async () => await behavior.Handle(AStage(), Ran(ran), default);

        (await refuse.Should().ThrowAsync<ForbiddenAccessException>())
            .Which.Message.Should().Contain("agency owner");
        ran.Value.Should().BeFalse();
    }

    [Fact]
    public async Task TheAgencyOwnerGetsThrough()
    {
        var ran = new Box();
        var behavior = Behavior<CreateStageCommand>(false, [AgencyRoles.Owner]);

        await behavior.Handle(AStage(), Ran(ran), default);

        ran.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ThePlatformTeamGetsThrough()
    {
        var ran = new Box();
        var behavior = Behavior<CreateStageCommand>(true, []);

        await behavior.Handle(AStage(), Ran(ran), default);

        ran.Value.Should().BeTrue();
    }

    [Fact]
    public async Task RemovingAStageIsHeldToTheSameGate()
    {
        // Removal is the one that cannot be undone from a board, so it matters most that the
        // gate is on the request type and not on a handler someone could add a sibling to.
        var ran = new Box();
        var behavior = Behavior<DeleteStageCommand>(false, ["EmbassyOfficer"], "workflow.configure");

        var refuse = async () =>
            await behavior.Handle(new DeleteStageCommand(Guid.NewGuid()), Ran(ran), default);

        await refuse.Should().ThrowAsync<ForbiddenAccessException>();
        ran.Value.Should().BeFalse();
    }

    [Fact]
    public async Task MovingCandidatesIsStillOrdinaryWork()
    {
        // The counterpart: using the workflow is what the staff are here to do, and it stays on
        // a permission. Locking this behind the owner would stop the agency functioning.
        var ran = new Box();
        var behavior = Behavior<ExecuteTransitionCommand>(
            false, ["EmbassyOfficer"], "workflow.execute");

        await behavior.Handle(
            new ExecuteTransitionCommand(Guid.NewGuid(), Guid.NewGuid(), null), Ran(ran), default);

        ran.Value.Should().BeTrue();
    }

    [Theory]
    [InlineData("AgencyOwner", true)]
    [InlineData("agencyowner", false)]
    [InlineData("OfficeManager", false)]
    [InlineData("", false)]
    public void OwnershipIsAnExactRoleName(string role, bool expected)
    {
        // Case-insensitive matching would hand the agency to anyone who could name a role.
        AgencyRoles.Owns([role]).Should().Be(expected);
    }
}
