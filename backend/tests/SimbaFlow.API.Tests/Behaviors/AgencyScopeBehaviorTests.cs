using FluentAssertions;
using MediatR;
using NSubstitute;
using SimbaFlow.API.Features.Compliance.Queries;
using SimbaFlow.API.Features.Dashboard.Queries;
using SimbaFlow.API.Features.Tasks.Queries;
using SimbaFlow.API.Features.Workflow.Queries;
using SimbaFlow.Application.Common.Behaviors;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// A platform administrator on "All agencies" has no tenant, and the agency-only reads used to run
/// their SQL anyway — `relation "candidates" does not exist`, a 500 per dashboard panel.
/// </summary>
public class AgencyScopeBehaviorTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private async Task<(TResponse Result, bool ReachedHandler)> Send<TRequest, TResponse>(
        TRequest request, TResponse fromHandler)
        where TRequest : notnull
    {
        var reached = false;
        var behavior = new AgencyScopeBehavior<TRequest, TResponse>(_currentUser);
        var result = await behavior.Handle(request, _ =>
        {
            reached = true;
            return Task.FromResult(fromHandler);
        }, CancellationToken.None);
        return (result, reached);
    }

    [Fact]
    public async Task NoAgency_AnswersEmpty_WithoutReachingTheHandler()
    {
        _currentUser.TenantId.Returns((Guid?)null);
        _currentUser.IsSuperAdmin.Returns(true);

        var (funnel, f) = await Send(new GetPipelineFunnelQuery(),
            Result<PipelineFunnelResult>.Failure("handler ran", 500));
        var (metrics, m) = await Send(new GetDashboardMetricsQuery(),
            Result<DashboardMetricsResult>.Failure("handler ran", 500));
        var (trends, t) = await Send(new GetDashboardTrendsQuery(),
            Result<List<TrendPointDto>>.Failure("handler ran", 500));
        var (tasks, k) = await Send(new GetMyTasksQuery(),
            Result<MyTasksResult>.Failure("handler ran", 500));
        var (alerts, a) = await Send(new GetComplianceAlertsQuery(),
            Result<ComplianceAlertsResult>.Failure("handler ran", 500));
        var (workflow, w) = await Send(new GetWorkflowDefinitionQuery(),
            Result<WorkflowDefinitionDto>.Failure("handler ran", 500));

        new[] { f, m, t, k, a, w }.Should().AllBeEquivalentTo(false);

        funnel.IsSuccess.Should().BeTrue();
        funnel.Data!.Stages.Should().BeEmpty();
        funnel.Data.TotalCandidates.Should().Be(0);
        metrics.Data.Should().Be(new DashboardMetricsResult(0, 0, 0m, 0, 0));
        trends.Data.Should().BeEmpty();
        tasks.Data!.Items.Should().BeEmpty();
        alerts.Data!.Alerts.Should().BeEmpty();

        // No pipeline to show: the same 404 an agency without one gets, not a 500.
        workflow.IsSuccess.Should().BeFalse();
        workflow.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task InsideAnAgency_RunsTheHandler()
    {
        _currentUser.TenantId.Returns(Guid.NewGuid());

        var fromHandler = Result<DashboardMetricsResult>.Success(new DashboardMetricsResult(5, 1, 0m, 0, 0));
        var (result, reached) = await Send(new GetDashboardMetricsQuery(), fromHandler);

        reached.Should().BeTrue();
        result.Should().BeSameAs(fromHandler);
    }

    [Fact]
    public async Task UnmarkedRequest_WithNoAgency_RunsTheHandler()
    {
        _currentUser.TenantId.Returns((Guid?)null);

        var (result, reached) = await Send(new PlainQuery(), Result<string>.Success("ok"));

        reached.Should().BeTrue();
        result.Data.Should().Be("ok");
    }

    private record PlainQuery : IRequest<Result<string>>;
}
