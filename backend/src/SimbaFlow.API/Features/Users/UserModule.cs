using Carter;
using MediatR;
using SimbaFlow.API.Features.Users.Commands;
using SimbaFlow.API.Features.Users.Queries;

namespace SimbaFlow.API.Features.Users;

public class UserModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute
            {
                AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme
            });

        group.MapGet("/", async (ISender sender, int page = 1, int pageSize = 20, string? search = null) =>
        {
            var result = await sender.Send(new GetUsersQuery(page, pageSize, search));
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetUserByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/{id:guid}/roles", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetUserRolesQuery(id));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/", async (CreateUserCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/users/{result.Data}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserCommand command, ISender sender) =>
        {
            var updated = command with { Id = id };
            var result = await sender.Send(updated);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPut("/{id:guid}/roles", async (Guid id, AssignRolesRequest request, ISender sender) =>
        {
            var command = new AssignRolesCommand(id, request.RoleNames, request.RoleIds);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPut("/{id:guid}/toggle-status", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new ToggleUserStatusCommand(id));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPut("/{id:guid}/force-logout", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new ForceLogoutCommand(id));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteUserCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.Json(result, statusCode: result.StatusCode);
        });

        // Admin reset user password
        group.MapPost("/{id:guid}/password", async (Guid id, ResetPasswordRequest request, ISender sender) =>
        {
            var result = await sender.Send(new ResetUserPasswordCommand(id, request.NewPassword));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}

public record AssignRolesRequest(List<string>? RoleNames, List<Guid>? RoleIds);
public record ResetPasswordRequest(string NewPassword);
