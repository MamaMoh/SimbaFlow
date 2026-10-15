using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Offices;

public sealed class OfficeBody
{
    public string? Name { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public bool? IsActive { get; set; }
}

public record OfficeDto(Guid Id, string Name, string? City, string? Phone, bool IsActive, int Candidates);

/// <summary>
/// The agency's own branches.
///
/// Read by anyone signed in to the agency, because registering a candidate means picking one;
/// changed only by someone who can change settings.
/// </summary>
public class OfficeModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/offices")
            .WithTags("Offices")
            .RequireAuthorization();

        group.MapGet("/", async (ITenantDbContext db, CancellationToken ct) =>
        {
            var rows = await ListAsync(db, ct);
            return Results.Ok(new { isSuccess = true, data = rows });
        });

        group.MapPost("/", async (
            OfficeBody body, ITenantDbContext db, ICurrentUserService user, CancellationToken ct) =>
        {
            if (!CanManage(user)) return Forbidden();

            var (fields, error) = Clean(body);
            if (error is not null) return BadRequest(error);

            if (await NameTakenAsync(db, fields.Name, null, ct))
                return Conflict($"There is already an office called {fields.Name}");

            var office = new Office
            {
                Name = fields.Name,
                City = fields.City,
                Phone = fields.Phone,
                IsActive = body.IsActive ?? true,
            };
            db.Offices.Add(office);
            await db.SaveChangesAsync(ct);

            return Results.Json(new { isSuccess = true, data = office.Id }, statusCode: 201);
        });

        group.MapPut("/{id:guid}", async (
            Guid id, OfficeBody body, ITenantDbContext db, ICurrentUserService user, CancellationToken ct) =>
        {
            if (!CanManage(user)) return Forbidden();

            var office = await db.Offices.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);
            if (office is null) return NotFound();

            var (fields, error) = Clean(body);
            if (error is not null) return BadRequest(error);

            if (await NameTakenAsync(db, fields.Name, id, ct))
                return Conflict($"There is already an office called {fields.Name}");

            office.Name = fields.Name;
            office.City = fields.City;
            office.Phone = fields.Phone;
            if (body.IsActive is bool active) office.IsActive = active;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { isSuccess = true });
        });

        group.MapDelete("/{id:guid}", async (
            Guid id, ITenantDbContext db, ICurrentUserService user, CancellationToken ct) =>
        {
            if (!CanManage(user)) return Forbidden();

            var office = await db.Offices.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);
            if (office is null) return NotFound();

            // Deleting an office with candidates on it would quietly move them all to "No office"
            // in every report. Closing it (inactive) keeps the history and stops new registrations.
            if (await db.Candidates.AnyAsync(c => c.OfficeId == id && !c.IsDeleted, ct))
                return Conflict("This office has candidates. Mark it inactive instead, or move them first.");

            office.IsDeleted = true;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    internal static async Task<List<OfficeDto>> ListAsync(ITenantDbContext db, CancellationToken ct)
    {
        var offices = await db.Offices.AsNoTracking()
            .Where(o => !o.IsDeleted)
            .OrderBy(o => o.Name)
            .ToListAsync(ct);

        var counts = await db.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active && c.OfficeId != null)
            .GroupBy(c => c.OfficeId!.Value)
            .Select(g => new { OfficeId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var countMap = counts.ToDictionary(x => x.OfficeId, x => x.Count);

        return offices
            .Select(o => new OfficeDto(o.Id, o.Name, o.City, o.Phone, o.IsActive,
                countMap.GetValueOrDefault(o.Id, 0)))
            .ToList();
    }

    private record OfficeFields(string Name, string? City, string? Phone);

    private static (OfficeFields Fields, string? Error) Clean(OfficeBody body)
    {
        var name = (body.Name ?? string.Empty).Trim();
        var city = string.IsNullOrWhiteSpace(body.City) ? null : body.City.Trim();
        var phone = string.IsNullOrWhiteSpace(body.Phone) ? null : body.Phone.Trim();
        var fields = new OfficeFields(name, city, phone);

        if (name.Length == 0) return (fields, "Give the office a name");
        if (name.Length > 128) return (fields, "Office name is too long");
        if (city?.Length > 128) return (fields, "City is too long");
        if (phone?.Length > 64) return (fields, "Phone number is too long");
        return (fields, null);
    }

    private static Task<bool> NameTakenAsync(ITenantDbContext db, string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.ToLower();
        return db.Offices.AnyAsync(o => !o.IsDeleted && o.Id != exceptId && o.Name.ToLower() == lower, ct);
    }

    private static bool CanManage(ICurrentUserService user)
        => user.IsSuperAdmin || user.HasPermission("settings.write");

    private static IResult Forbidden() => Results.Json(
        new { isSuccess = false, error = "You cannot change the agency's offices" }, statusCode: 403);

    private static IResult NotFound() => Results.Json(
        new { isSuccess = false, error = "Office not found" }, statusCode: 404);

    private static IResult BadRequest(string error) => Results.Json(
        new { isSuccess = false, error }, statusCode: 400);

    private static IResult Conflict(string error) => Results.Json(
        new { isSuccess = false, error }, statusCode: 409);
}
