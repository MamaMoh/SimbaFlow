using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Tenants.Commands;
using SimbaFlow.API.Features.Tenants.Queries;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Features.Tenants;

public class TenantModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants")
            .WithTags("Tenants")
            .RequireAuthorization("SuperAdmin");

        // List all tenants
        group.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetTenantsQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Get tenant by ID
        group.MapGet("/{id:guid}", async (Guid id, IApplicationDbContext context) =>
        {
            var tenant = await context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Tenant not found" }, statusCode: 404);

            // Find the agency owner (first user with this TenantId and AgencyOwner role)
            var owner = await context.ApplicationUsers
                .AsNoTracking()
                .Where(u => u.TenantId == id && !u.IsDeleted)
                .OrderBy(u => u.CreatedAt)
                .Select(u => new { u.FirstName, u.LastName, u.Email })
                .FirstOrDefaultAsync();

            return Results.Ok(new
            {
                isSuccess = true,
                data = new
                {
                    tenant.Id,
                    tenant.Name,
                    tenant.Slug,
                    tenant.SchemaName,
                    tenant.ContactEmail,
                    tenant.ContactPhone,
                    Status = (int)tenant.SubscriptionStatus,
                    tenant.MaxUsers,
                    tenant.ProvisionedAt,
                    OwnerFirstName = owner?.FirstName,
                    OwnerLastName = owner?.LastName,
                    OwnerEmail = owner?.Email,
                }
            });
        });

        // Create tenant
        group.MapPost("/", async (ProvisionTenantCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/tenants/{result.Data}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        // Update tenant details
        group.MapPut("/{id:guid}", async (Guid id, UpdateTenantRequest request, IApplicationDbContext context) =>
        {
            var tenant = await context.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Tenant not found" }, statusCode: 404);

            tenant.Name = request.Name;
            tenant.ContactEmail = request.ContactEmail;
            tenant.ContactPhone = request.ContactPhone;
            tenant.MaxUsers = request.MaxUsers ?? tenant.MaxUsers;

            await context.SaveChangesAsync();
            return Results.Ok(new { isSuccess = true });
        });

        // Update tenant status
        group.MapPut("/{id:guid}/status", async (Guid id, UpdateTenantStatusRequest request, ISender sender) =>
        {
            var command = new UpdateTenantStatusCommand(id, request.Status);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Delete tenant (soft delete)
        group.MapDelete("/{id:guid}", async (Guid id, IApplicationDbContext context) =>
        {
            var tenant = await context.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Tenant not found" }, statusCode: 404);

            tenant.IsDeleted = true;
            tenant.SubscriptionStatus = Domain.Enums.TenantStatus.Deactivated;
            await context.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}

public record UpdateTenantStatusRequest(Domain.Enums.TenantStatus Status);
public record UpdateTenantRequest(string Name, string ContactEmail, string? ContactPhone, int? MaxUsers);
