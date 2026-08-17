using Carter;
using MediatR;
using SimbaFlow.API.Features.Dashboard.Queries;

namespace SimbaFlow.API.Features.Dashboard;

public class DashboardModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();

        group.MapGet("/pipeline-funnel", async (ISender sender) =>
        {
            var result = await sender.Send(new GetPipelineFunnelQuery());
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/metrics", async (ISender sender) =>
        {
            var result = await sender.Send(new GetDashboardMetricsQuery());
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/trends", async (ISender sender) =>
        {
            var result = await sender.Send(new GetDashboardTrendsQuery());
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}
