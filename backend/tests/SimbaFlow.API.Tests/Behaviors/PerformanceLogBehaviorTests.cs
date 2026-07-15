using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SimbaFlow.Application.Common.Behaviors;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Tests.Behaviors;

public class PerformanceLogBehaviorTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    [Fact]
    public async Task Handle_FastRequest_PassesThrough()
    {
        // Arrange
        var logger = NullLogger<PerformanceLogBehavior<TestQuery, Result<string>>>.Instance;
        var behavior = new PerformanceLogBehavior<TestQuery, Result<string>>(logger, _currentUser);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next.Invoke(Arg.Any<CancellationToken>()).Returns(Result<string>.Success("ok"));

        // Act
        var result = await behavior.Handle(new TestQuery(), next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_SlowRequest_StillReturnsResult()
    {
        // Arrange
        var logger = NullLogger<PerformanceLogBehavior<TestQuery, Result<string>>>.Instance;
        var behavior = new PerformanceLogBehavior<TestQuery, Result<string>>(logger, _currentUser);
        _currentUser.UserId.Returns("user-123");

        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next.Invoke(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await Task.Delay(600); // Exceed 500ms threshold
            return Result<string>.Success("slow");
        });

        // Act
        var result = await behavior.Handle(new TestQuery(), next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("slow");
    }

    private record TestQuery : IRequest<Result<string>>;
}
