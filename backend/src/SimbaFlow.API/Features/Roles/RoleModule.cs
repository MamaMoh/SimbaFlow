using Carter;
using MediatR;
using SimbaFlow.API.Features.Roles.Commands;
using SimbaFlow.API.Features.Roles.Queries;

namespace SimbaFlow.API.Features.Roles;

/// <summary>
/// Roles and their permissions.
///
/// Every route dispatches through MediatR, which is the only way a request meets
/// AuthorizationBehavior and so the only way IRequirePermission is enforced. The previous version
/// of this module was written inline against the DbContext, which bypassed that pipeline entirely
/// and left role administration — create, rewrite, delete — open to any signed-in user.
///
/// It also wrote to a second set of per-tenant RBAC tables that nothing ever read: tokens are built
/// from the platform-schema RolePermissions joined on Identity roles. Editing a role there changed
/// what this screen displayed and nothing else, so an administrator who removed a permission would
/// have believed they had revoked access the user still had. These routes now read and write the
/// tables that actually decide access.
/// </summary>
public class RoleModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/roles")
            .WithTags("Roles & Permissions")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetRolesQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // The permission catalogue, flattened. The query groups by module for callers that want
        // that shape; the roles screen does its own grouping, so it is given the flat list.
        group.MapGet("/permissions", async (ISender sender) =>
        {
            var result = await sender.Send(new GetPermissionsQuery());
            if (!result.IsSuccess)
                return Results.Json(result, statusCode: result.StatusCode);

            var flattened = (result.Data ?? [])
                .SelectMany(g => g.Permissions.Select(p => new
                {
                    p.Id,
                    p.Code,
                    p.Name,
                    Module = g.Module,
                }))
                .ToList();

            return Results.Ok(new { isSuccess = true, data = flattened });
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetRoleByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/", async (CreateRoleRequest request, ISender sender) =>
        {
            var result = await sender.Send(
                new CreateRoleCommand(request.Name, request.Description, request.PermissionIds ?? []));
            return result.IsSuccess
                ? Results.Created($"/api/roles/{result.Data}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateRoleRequest request, ISender sender) =>
        {
            var result = await sender.Send(
                new UpdateRoleCommand(id, request.Name, request.Description, request.PermissionIds ?? []));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteRoleCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.Json(result, statusCode: result.StatusCode);
        });

        // Role membership is not managed here. It belongs to a user, and lives at
        // PUT /api/users/{id}/roles — which is where the checks that stop someone granting
        // themselves a platform role are applied.
    }
}

public record CreateRoleRequest(string Name, string? Description, List<Guid>? PermissionIds);
public record UpdateRoleRequest(string Name, string? Description, List<Guid>? PermissionIds);
