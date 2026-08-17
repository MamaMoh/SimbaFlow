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
    }
}
