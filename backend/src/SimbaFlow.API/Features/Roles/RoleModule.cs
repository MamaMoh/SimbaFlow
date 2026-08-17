using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Tenancy;

namespace SimbaFlow.API.Features.Roles;

public class RoleModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/roles")
            .WithTags("Roles & Permissions")
            .RequireAuthorization();

        // List all roles for the current tenant
        group.MapGet("/", async (ITenantDbContext context) =>
        {
            var roles = await context.TenantRoles
                .AsNoTracking()
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.SortOrder)
                .Select(r => new RoleDto(
                    r.Id, r.Name, r.Code, r.Description,
                    r.IsSystemRole, r.IsActive, r.SortOrder,
                    r.Permissions.Select(p => p.PermissionCode).ToList(),
                    r.UserRoles.Count))
                .ToListAsync();

            return Results.Ok(new { isSuccess = true, data = roles });
        });

        // Get all system permissions (the building blocks for role assignment)
        group.MapGet("/permissions", async (IPlatformDbContext context) =>
        {
            var permissions = await context.Permissions
                .AsNoTracking()
                .Where(p => p.IsActive && !p.IsDeleted)
                .OrderBy(p => p.Module)
                .ThenBy(p => p.Code)
                .Select(p => new PermissionDto(p.Id, p.Code, p.Name, p.Module))
                .ToListAsync();

            return Results.Ok(new { isSuccess = true, data = permissions });
        });

        // Create a new role for the current tenant
        group.MapPost("/", async (CreateRoleRequest request, ITenantDbContext context) =>
        {
            // Check code uniqueness
            var exists = await context.TenantRoles
                .AnyAsync(r => r.Code == request.Code && !r.IsDeleted);
            if (exists)
                return Results.Json(new { isSuccess = false, error = "A role with this code already exists" }, statusCode: 409);

            var role = new TenantRole
            {
                Name = request.Name,
                Code = request.Code,
                Description = request.Description,
                IsSystemRole = false,
                IsActive = true,
                SortOrder = request.SortOrder,
            };

            // Assign permissions
            if (request.Permissions?.Count > 0)
            {
                foreach (var permCode in request.Permissions)
                {
                    role.Permissions.Add(new TenantRolePermission
                    {
                        TenantRoleId = role.Id,
                        PermissionCode = permCode,
                    });
                }
            }

            context.TenantRoles.Add(role);
            await context.SaveChangesAsync();

            return Results.Created($"/api/roles/{role.Id}", new { isSuccess = true, data = role.Id });
        });

        // Update role
        group.MapPut("/{id:guid}", async (Guid id, UpdateRoleRequest request, ITenantDbContext context) =>
        {
            var role = await context.TenantRoles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (role is null)
                return Results.Json(new { isSuccess = false, error = "Role not found" }, statusCode: 404);

            role.Name = request.Name;
            role.Description = request.Description;
            role.SortOrder = request.SortOrder;
            role.IsActive = request.IsActive;

            // Replace permissions
            role.Permissions.Clear();
            if (request.Permissions?.Count > 0)
            {
                foreach (var permCode in request.Permissions)
                {
                    role.Permissions.Add(new TenantRolePermission
                    {
                        TenantRoleId = role.Id,
                        PermissionCode = permCode,
                    });
                }
            }

            await context.SaveChangesAsync();
            return Results.Ok(new { isSuccess = true });
        });

        // Delete role
        group.MapDelete("/{id:guid}", async (Guid id, ITenantDbContext context) =>
        {
            var role = await context.TenantRoles
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (role is null)
                return Results.Json(new { isSuccess = false, error = "Role not found" }, statusCode: 404);

            if (role.IsSystemRole)
                return Results.Json(new { isSuccess = false, error = "Cannot delete system roles" }, statusCode: 400);

            role.IsDeleted = true;
            await context.SaveChangesAsync();

            return Results.NoContent();
        });

        // Assign role to user
        group.MapPost("/{roleId:guid}/users/{userId:guid}", async (Guid roleId, Guid userId, ITenantDbContext context) =>
        {
            var exists = await context.TenantUserRoles
                .AnyAsync(ur => ur.UserId == userId && ur.TenantRoleId == roleId);

            if (exists)
                return Results.Ok(new { isSuccess = true, message = "User already has this role" });

            context.TenantUserRoles.Add(new TenantUserRole
            {
                UserId = userId,
                TenantRoleId = roleId,
            });
            await context.SaveChangesAsync();

            return Results.Ok(new { isSuccess = true });
        });

        // Remove role from user
        group.MapDelete("/{roleId:guid}/users/{userId:guid}", async (Guid roleId, Guid userId, ITenantDbContext context) =>
        {
            var assignment = await context.TenantUserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.TenantRoleId == roleId);

            if (assignment is null)
                return Results.Json(new { isSuccess = false, error = "Assignment not found" }, statusCode: 404);

            context.TenantUserRoles.Remove(assignment);
            await context.SaveChangesAsync();

            return Results.NoContent();
        });

        // Get users for a role
        group.MapGet("/{roleId:guid}/users", async (Guid roleId, ITenantDbContext tenantContext, IPlatformDbContext platformContext) =>
        {
            var userRoles = await tenantContext.TenantUserRoles
                .AsNoTracking()
                .Where(ur => ur.TenantRoleId == roleId)
                .ToListAsync();

            var userIds = userRoles.Select(ur => ur.UserId).ToList();

            var users = await platformContext.ApplicationUsers
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    FullName = u.FirstName + " " + u.LastName,
                    u.Email,
                })
                .ToListAsync();

            var result = users.Select(u =>
            {
                var ur = userRoles.First(x => x.UserId == u.Id);
                return new
                {
                    u.Id,
                    u.UserName,
                    u.FullName,
                    u.Email,
                    ur.AssignedAt,
                };
            }).ToList();

            return Results.Ok(new { isSuccess = true, data = result });
        });
    }
}

// DTOs
public record RoleDto(Guid Id, string Name, string Code, string? Description, bool IsSystemRole, bool IsActive, int SortOrder, List<string> Permissions, int UserCount);
public record PermissionDto(Guid Id, string Code, string Name, string Module);
public record CreateRoleRequest(string Name, string Code, string? Description, int SortOrder, List<string>? Permissions);
public record UpdateRoleRequest(string Name, string? Description, int SortOrder, bool IsActive, List<string>? Permissions);
