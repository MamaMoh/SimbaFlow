using Carter;
using MediatR;
using SimbaFlow.API.Features.Departments.Commands;
using SimbaFlow.API.Features.Departments.Queries;

namespace SimbaFlow.API.Features.Departments;

public class DepartmentModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/departments")
            .WithTags("Departments")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetDepartmentsQuery());
            return Results.Ok(result);
        });

        group.MapPost("/", async (CreateDepartmentCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/departments/{result.Data}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateDepartmentCommand command, ISender sender) =>
        {
            var updated = command with { Id = id };
            var result = await sender.Send(updated);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteDepartmentCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}
