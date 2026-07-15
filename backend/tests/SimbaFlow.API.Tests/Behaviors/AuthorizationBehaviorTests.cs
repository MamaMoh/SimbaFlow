using FluentAssertions;
using MediatR;
using NSubstitute;
using SimbaFlow.Application.Common.Behaviors;
using SimbaFlow.Application.Common.Exceptions;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Tests.Behaviors;

public class AuthorizationBehaviorTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly RequestHandlerDelegate<Result<Guid>> _next = Substitute.For<RequestHandlerDelegate<Result<Guid>>>();

    private AuthorizationBehavior<TestCommand, Result<Guid>> CreateBehavior() =>
        new(_currentUser);

    [Fact]
    public async Task Handle_NoPermissionInterface_PassesThrough()
    {
        // Arrange
        var behavior = new AuthorizationBehavior<TestCommandNoPermission, Result<Guid>>(_currentUser);
        var request = new TestCommandNoPermission();
        var nextNoPermission = Substitute.For<RequestHandlerDelegate<Result<Guid>>>();
        nextNoPermission.Invoke(Arg.Any<CancellationToken>()).Returns(Result<Guid>.Success(Guid.NewGuid()));

        // Act
        var result = await behavior.Handle(request, nextNoPermission, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await nextNoPermission.Received(1).Invoke(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuperAdmin_BypassesPermissionCheck()
    {
        // Arrange
        var behavior = CreateBehavior();
        var request = new TestCommand();
        _currentUser.IsSuperAdmin.Returns(true);
        _next.Invoke(Arg.Any<CancellationToken>()).Returns(Result<Guid>.Success(Guid.NewGuid()));

        // Act
        var result = await behavior.Handle(request, _next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _next.Received(1).Invoke(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserHasPermission_PassesThrough()
    {
        // Arrange
        var behavior = CreateBehavior();
        var request = new TestCommand();
        _currentUser.IsSuperAdmin.Returns(false);
        _currentUser.UserId.Returns("user-123");
        _currentUser.HasPermission("test.write").Returns(true);
        _next.Invoke(Arg.Any<CancellationToken>()).Returns(Result<Guid>.Success(Guid.NewGuid()));

        // Act
        var result = await behavior.Handle(request, _next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UserLacksPermission_ThrowsForbidden()
    {
        // Arrange
        var behavior = CreateBehavior();
        var request = new TestCommand();
        _currentUser.IsSuperAdmin.Returns(false);
        _currentUser.UserId.Returns("user-123");
        _currentUser.HasPermission("test.write").Returns(false);

        // Act
        var act = () => behavior.Handle(request, _next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*test.write*");
    }

    [Fact]
    public async Task Handle_NoUserId_ThrowsUnauthorized()
    {
        // Arrange
        var behavior = CreateBehavior();
        var request = new TestCommand();
        _currentUser.IsSuperAdmin.Returns(false);
        _currentUser.UserId.Returns((string?)null);

        // Act
        var act = () => behavior.Handle(request, _next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Authentication required*");
    }

    // Test helpers
    private record TestCommand : IRequest<Result<Guid>>, IRequirePermission
    {
        public string RequiredPermission => "test.write";
    }

    private record TestCommandNoPermission : IRequest<Result<Guid>>;
}
