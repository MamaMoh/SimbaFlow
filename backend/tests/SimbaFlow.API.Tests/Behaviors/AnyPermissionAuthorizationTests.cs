using FluentAssertions;
using MediatR;
using NSubstitute;
using SimbaFlow.API.Features.Candidates.Commands;
using SimbaFlow.Application.Common.Behaviors;
using SimbaFlow.Application.Common.Exceptions;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Actions more than one desk performs.
///
/// Filing a document is done by the clerk who owns the candidate's record and by the officer who
/// works the LMIS board. Neither holds the other's permission, so naming a single one would lock a
/// desk out of its own work — and naming none, which is how this endpoint shipped, lets an auditor
/// write files onto anybody's record.
/// </summary>
public class AnyPermissionAuthorizationTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private AuthorizationBehavior<T, Result<Guid>> BehaviorFor<T>() where T : notnull => new(_currentUser);

    private static RequestHandlerDelegate<Result<Guid>> Reached(out Func<bool> wasReached)
    {
        var hit = false;
        wasReached = () => hit;
        return _ =>
        {
            hit = true;
            return Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        };
    }

    private record TwoDeskCommand : IRequest<Result<Guid>>, IRequireAnyPermission
    {
        public IReadOnlyList<string> AcceptedPermissions => ["candidate.update", "lmis.document"];
    }

    private record NoPermissionsListed : IRequest<Result<Guid>>, IRequireAnyPermission
    {
        public IReadOnlyList<string> AcceptedPermissions => [];
    }

    [Theory]
    [InlineData("candidate.update")]
    [InlineData("lmis.document")]
    public async Task EitherDesksPermissionAdmitsTheCaller(string held)
    {
        _currentUser.UserId.Returns("user-1");
        _currentUser.HasPermission(held).Returns(true);

        var next = Reached(out var reached);
        await BehaviorFor<TwoDeskCommand>().Handle(new TwoDeskCommand(), next, default);

        reached().Should().BeTrue();
    }

    [Fact]
    public async Task HoldingNeitherIsRefused()
    {
        // An auditor: reads everything, writes nothing.
        _currentUser.UserId.Returns("user-1");
        _currentUser.HasPermission(Arg.Any<string>()).Returns(false);

        var act = async () =>
            await BehaviorFor<TwoDeskCommand>().Handle(new TwoDeskCommand(), Reached(out _), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task AnEmptyListRefusesRatherThanAdmittingEveryone()
    {
        // Otherwise an action looks gated and is not, which is the failure this whole change is about.
        _currentUser.UserId.Returns("user-1");

        var act = async () =>
            await BehaviorFor<NoPermissionsListed>().Handle(new NoPermissionsListed(), Reached(out _), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task SuperAdminBypasses()
    {
        _currentUser.IsSuperAdmin.Returns(true);

        var next = Reached(out var reached);
        await BehaviorFor<TwoDeskCommand>().Handle(new TwoDeskCommand(), next, default);

        reached().Should().BeTrue();
    }

    [Fact]
    public async Task SignedOutIsUnauthorizedRatherThanForbidden()
    {
        _currentUser.UserId.Returns((string?)null);

        var act = async () =>
            await BehaviorFor<TwoDeskCommand>().Handle(new TwoDeskCommand(), Reached(out _), default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public void UploadingADocumentIsGated()
    {
        // The endpoint sits behind RequireAuthorization() with the rest of the candidates group,
        // which is authentication, not authorization — this is the part that says who may write.
        var command = new UploadDocumentCommand(Guid.NewGuid(), null!, 0);

        command.AcceptedPermissions.Should().Contain("candidate.update");
        command.AcceptedPermissions.Should().Contain("lmis.document");
    }
}
