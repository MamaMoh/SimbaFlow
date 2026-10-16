using Carter;
using MediatR;
using SimbaFlow.API.Features.Tasks.Queries;

namespace SimbaFlow.API.Features.Tasks;

public class TaskModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks")
            .WithTags("Tasks")
            .RequireAuthorization();

        group.MapGet("/mine", async (ISender sender) =>
        {
            var result = await sender.Send(new GetMyTasksQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/queues", async (
            ISender sender, string? search, string? partner, Guid? office) =>
        {
            var result = await sender.Send(new GetMyWorkQueuesQuery(search, partner, office));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}
