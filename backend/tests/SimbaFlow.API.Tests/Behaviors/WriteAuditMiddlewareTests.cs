using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using NSubstitute;
using SimbaFlow.API.Middleware;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Endpoints that write without going through MediatR — partners, offices, branding, bot
/// settings, intake defaults — left nothing on the audit trail. The catch-all records them.
/// </summary>
public class WriteAuditMiddlewareTests
{
    private readonly IAuditService _audit = Substitute.For<IAuditService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly Guid _tenant = Guid.NewGuid();

    public WriteAuditMiddlewareTests()
    {
        _currentUser.UserId.Returns("user-1");
        _currentUser.TenantId.Returns(_tenant);
    }

    private static HttpContext Request(string method, string route, object? id = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/api/offices/1";
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0, EndpointMetadataCollection.Empty, route));
        if (id is not null) context.Request.RouteValues["id"] = id;
        return context;
    }

    private Task Invoke(HttpContext context, RequestDelegate endpoint) =>
        new WriteAuditMiddleware(endpoint).InvokeAsync(context, _audit, _currentUser);

    private AuditEntry Logged() =>
        (AuditEntry)_audit.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IAuditService.LogAsync))
            .GetArguments()[0]!;

    [Fact]
    public async Task AWriteNothingElseRecorded_IsRecordedByItsRoute()
    {
        var id = Guid.NewGuid();
        var context = Request("PUT", "/api/offices/{id:guid}", id);

        await Invoke(context, c => { c.Response.StatusCode = 200; return Task.CompletedTask; });

        var entry = Logged();
        entry.Action.Should().Be("HTTP PUT /api/offices/{id}");
        entry.EntityType.Should().Be("offices");
        entry.EntityId.Should().Be(id.ToString());
        entry.TenantId.Should().Be(_tenant);
        entry.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ARefusedWrite_IsRecordedAsAFailure()
    {
        await Invoke(Request("DELETE", "/api/offices/{id:guid}", Guid.NewGuid()),
            c => { c.Response.StatusCode = 403; return Task.CompletedTask; });

        Logged().Success.Should().BeFalse();
    }

    [Fact]
    public async Task AWriteThatThrew_IsRecordedAsAFailure()
    {
        var act = () => Invoke(Request("POST", "/api/offices"), _ => throw new InvalidOperationException());

        await act.Should().ThrowAsync<InvalidOperationException>();
        Logged().Success.Should().BeFalse();
    }

    [Fact]
    public async Task AWriteAlreadyRecorded_IsNotRecordedTwice()
    {
        _audit.HasLogged.Returns(true);

        await Invoke(Request("POST", "/api/offices"), _ => Task.CompletedTask);

        _audit.DidNotReceiveWithAnyArgs().LogAsync(default!, default);
    }

    [Fact]
    public async Task ReadsAndAnonymousCalls_AreNotRecorded()
    {
        await Invoke(Request("GET", "/api/offices"), _ => Task.CompletedTask);

        _currentUser.UserId.Returns((string?)null);
        await Invoke(Request("POST", "/api/bot/webhook"), _ => Task.CompletedTask);

        _audit.DidNotReceiveWithAnyArgs().LogAsync(default!, default);
    }

    [Fact]
    public void TokenRefreshes_AreExempt() =>
        WriteAuditMiddleware.IsExempt("/api/auth/refresh").Should().BeTrue();
}
