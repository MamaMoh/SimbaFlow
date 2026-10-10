using FluentAssertions;
using MediatR;
using NSubstitute;
using SimbaFlow.API.Features.Roles.Commands;
using SimbaFlow.API.Features.Roles.Queries;
using SimbaFlow.Application.Common.Behaviors;
using SimbaFlow.Application.Common.Exceptions;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The role catalogue belongs to the platform team.
///
/// It says what every permission in the system means and who may hold it — including the
/// permission to edit roles. An agency owner who could open it could grant their own account
/// anything, which is a lock whose key sits inside the room, so this gate is not a permission
/// and cannot be opened by granting one.
/// </summary>
public class PlatformOnlyRolesTests
{
    private static AuthorizationBehavior<TRequest, bool> Behavior<TRequest>(
        bool isSuperAdmin, params string[] permissions)
        where TRequest : notnull
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns("user-1");
        user.IsSuperAdmin.Returns(isSuperAdmin);
        user.Permissions.Returns(permissions);
        user.HasPermission(Arg.Any<string>())
            .Returns(call => isSuperAdmin || permissions.Contains(call.Arg<string>()));
        return new AuthorizationBehavior<TRequest, bool>(user);
    }

    private static RequestHandlerDelegate<bool> Ran(StrongBox ran) =>
        _ => { ran.Value = true; return Task.FromResult(true); };

    private sealed class StrongBox { public bool Value; }

    [Fact]
    public async Task AnAgencyOwnerHoldingEveryRolePermissionIsStillRefused()
    {
        var ran = new StrongBox();
        var behavior = Behavior<GetRolesQuery>(false, "role.read", "role.write", "users.write");

        var refuse = async () => await behavior.Handle(new GetRolesQuery(), Ran(ran), default);

        await refuse.Should().ThrowAsync<ForbiddenAccessException>();
        ran.Value.Should().BeFalse();
    }

    [Fact]
    public async Task CreatingARoleIsRefusedToo()
    {
        var ran = new StrongBox();
        var behavior = Behavior<CreateRoleCommand>(false, "role.write");

        var refuse = async () => await behavior.Handle(
            new CreateRoleCommand("Desk clerk", "", []), Ran(ran), default);

        await refuse.Should().ThrowAsync<ForbiddenAccessException>();
        ran.Value.Should().BeFalse();
    }

    [Fact]
    public async Task ThePlatformTeamStillGetsThrough()
    {
        var ran = new StrongBox();
        var behavior = Behavior<GetRolesQuery>(true);

        await behavior.Handle(new GetRolesQuery(), Ran(ran), default);

        ran.Value.Should().BeTrue();
    }
}
