using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Locations;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Locations;

public class LocationModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/locations")
            .WithTags("Locations")
            .RequireAuthorization("Permission:location.read");

        // List all locations (flat, filterable by type)
        group.MapGet("/", async (string? type, IApplicationDbContext context) =>
        {
            var query = context.Locations.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<LocationType>(type, true, out var locType))
                query = query.Where(l => l.Type == locType);

            var locations = await query
                .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
                .Select(l => new LocationDto(
                    l.Id, l.Name, l.Code, l.Type.ToString(),
                    l.Description, l.ParentLocationId,
                    l.ParentLocation != null ? l.ParentLocation.Name : null,
                    l.IsActive, l.Capacity, l.Floor, l.Building, l.SortOrder))
                .ToListAsync();

            return Results.Ok(new { isSuccess = true, data = locations });
        });

        // Get location tree (hierarchical)
        group.MapGet("/tree", async (IApplicationDbContext context) =>
        {
            var all = await context.Locations
                .AsNoTracking()
                .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
                .ToListAsync();

            var tree = BuildTree(all, null);
            return Results.Ok(new { isSuccess = true, data = tree });
        });

        // Get by ID
        group.MapGet("/{id:guid}", async (Guid id, IApplicationDbContext context) =>
        {
            var loc = await context.Locations
                .AsNoTracking()
                .Include(l => l.ParentLocation)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loc is null)
                return Results.NotFound(new { isSuccess = false, error = "Not found" });

            return Results.Ok(new
            {
                isSuccess = true,
                data = new LocationDto(
                    loc.Id, loc.Name, loc.Code, loc.Type.ToString(),
                    loc.Description, loc.ParentLocationId,
                    loc.ParentLocation?.Name, loc.IsActive,
                    loc.Capacity, loc.Floor, loc.Building, loc.SortOrder)
            });
        });

        // Create
        group.MapPost("/", async (CreateLocationRequest request, IApplicationDbContext context) =>
        {
            if (!Enum.TryParse<LocationType>(request.Type, true, out var locType))
                return Results.BadRequest(new { isSuccess = false, error = "Invalid location type" });

            var location = new Location
            {
                Name = request.Name,
                Code = request.Code,
                Type = locType,
                Description = request.Description,
                ParentLocationId = request.ParentLocationId,
                Capacity = request.Capacity,
                Floor = request.Floor,
                Building = request.Building,
                SortOrder = request.SortOrder ?? 0,
                IsActive = true,
            };

            context.Locations.Add(location);
            await context.SaveChangesAsync();

            return Results.Created($"/api/locations/{location.Id}",
                new { isSuccess = true, data = location.Id.ToString() });
        }).RequireAuthorization("Permission:location.write");

        // Update
        group.MapPut("/{id:guid}", async (Guid id, UpdateLocationRequest request, IApplicationDbContext context) =>
        {
            var location = await context.Locations.FirstOrDefaultAsync(l => l.Id == id);
            if (location is null)
                return Results.NotFound(new { isSuccess = false, error = "Not found" });

            if (!Enum.TryParse<LocationType>(request.Type, true, out var locType))
                return Results.BadRequest(new { isSuccess = false, error = "Invalid location type" });

            location.Name = request.Name;
            location.Code = request.Code;
            location.Type = locType;
            location.Description = request.Description;
            location.ParentLocationId = request.ParentLocationId;
            location.IsActive = request.IsActive;
            location.Capacity = request.Capacity;
            location.Floor = request.Floor;
            location.Building = request.Building;
            location.SortOrder = request.SortOrder ?? location.SortOrder;

            await context.SaveChangesAsync();
            return Results.Ok(new { isSuccess = true, data = id.ToString() });
        }).RequireAuthorization("Permission:location.write");

        // Delete
        group.MapDelete("/{id:guid}", async (Guid id, IApplicationDbContext context) =>
        {
            var location = await context.Locations.FirstOrDefaultAsync(l => l.Id == id);
            if (location is null)
                return Results.NotFound(new { isSuccess = false, error = "Not found" });

            location.IsDeleted = true;
            await context.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Permission:location.write");
    }

    private static List<LocationTreeDto> BuildTree(List<Location> all, Guid? parentId)
    {
        return all
            .Where(l => l.ParentLocationId == parentId)
            .Select(l => new LocationTreeDto(
                l.Id, l.Name, l.Code, l.Type.ToString(),
                l.IsActive, l.Capacity,
                BuildTree(all, l.Id)))
            .ToList();
    }
}

// --- DTOs ---
public record LocationDto(
    Guid Id, string Name, string Code, string Type,
    string? Description, Guid? ParentLocationId, string? ParentLocationName,
    bool IsActive, int? Capacity, string? Floor, string? Building, int SortOrder);

public record LocationTreeDto(
    Guid Id, string Name, string Code, string Type,
    bool IsActive, int? Capacity, List<LocationTreeDto> Children);

public record CreateLocationRequest(
    string Name, string Code, string Type, string? Description,
    Guid? ParentLocationId, int? Capacity, string? Floor, string? Building, int? SortOrder);

public record UpdateLocationRequest(
    string Name, string Code, string Type, string? Description,
    Guid? ParentLocationId, bool IsActive, int? Capacity,
    string? Floor, string? Building, int? SortOrder);
