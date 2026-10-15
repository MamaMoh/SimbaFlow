using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Platform;

namespace SimbaFlow.API.Features.Platform;

public record AnnouncementBody(
    string Title,
    string Body,
    string? Level,
    Guid? TenantId,
    DateTime? StartsAt,
    DateTime? EndsAt);

/// <summary>
/// Messages from the platform team to agencies: posted by any platform staff, shown to agency
/// staff as a banner while they are current.
/// </summary>
public class AnnouncementModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ──── What the signed-in person should see now ────
        app.MapGet("/api/announcements/active", async (
            IPlatformDbContext db, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var tenantId = currentUser.TenantId;
            var items = await db.Announcements.AsNoTracking()
                .Where(a => !a.IsDeleted && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
                .Where(a => a.TenantId == null || a.TenantId == tenantId)
                .OrderByDescending(a => a.Level).ThenByDescending(a => a.StartsAt)
                .Select(a => new { a.Id, a.Title, a.Body, Level = a.Level.ToString(), a.StartsAt, a.EndsAt })
                .ToListAsync(ct);
            return Results.Ok(new { isSuccess = true, data = items });
        }).WithTags("Announcements").RequireAuthorization();

        // ──── The platform team's list ────
        var staff = app.MapGroup("/api/platform/announcements")
            .WithTags("Announcements")
            .RequireAuthorization("PlatformStaff");

        staff.MapGet("/", async (IPlatformDbContext db, CancellationToken ct) =>
        {
            var tenants = await db.Tenants.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
            var rows = await db.Announcements.AsNoTracking()
                .Where(a => !a.IsDeleted)
                .OrderByDescending(a => a.StartsAt)
                .Take(200)
                .ToListAsync(ct);
            var now = DateTime.UtcNow;
            var data = rows.Select(a => new
            {
                a.Id, a.Title, a.Body, Level = a.Level.ToString(), a.TenantId,
                Agency = a.TenantId is Guid t ? tenants.GetValueOrDefault(t) : null,
                a.StartsAt, a.EndsAt, a.CreatedBy,
                IsLive = a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now),
            });
            return Results.Ok(new { isSuccess = true, data });
        });

        staff.MapPost("/", async (
            AnnouncementBody body, IPlatformDbContext db, IAuditService audit,
            ICurrentUserService currentUser, CancellationToken ct) =>
        {
            var announcement = new Announcement();
            if (Apply(announcement, body) is { } error)
                return Results.Json(new { isSuccess = false, error }, statusCode: 400);
            db.Announcements.Add(announcement);
            await db.SaveChangesAsync(ct);
            await PlatformAudit.LogAsync(audit, currentUser, "PostAnnouncement", announcement.TenantId,
                "Announcement", announcement.Id.ToString(), new { announcement.Title }, ct);
            return Results.Ok(new { isSuccess = true, data = new { announcement.Id } });
        });

        staff.MapPut("/{id:guid}", async (
            Guid id, AnnouncementBody body, IPlatformDbContext db, IAuditService audit,
            ICurrentUserService currentUser, CancellationToken ct) =>
        {
            var announcement = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);
            if (announcement is null)
                return Results.Json(new { isSuccess = false, error = "Announcement not found." }, statusCode: 404);
            if (Apply(announcement, body) is { } error)
                return Results.Json(new { isSuccess = false, error }, statusCode: 400);
            await db.SaveChangesAsync(ct);
            await PlatformAudit.LogAsync(audit, currentUser, "UpdateAnnouncement", announcement.TenantId,
                "Announcement", id.ToString(), new { announcement.Title }, ct);
            return Results.Ok(new { isSuccess = true });
        });

        staff.MapDelete("/{id:guid}", async (
            Guid id, IPlatformDbContext db, IAuditService audit,
            ICurrentUserService currentUser, CancellationToken ct) =>
        {
            var announcement = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);
            if (announcement is null)
                return Results.Json(new { isSuccess = false, error = "Announcement not found." }, statusCode: 404);
            announcement.IsDeleted = true;
            await db.SaveChangesAsync(ct);
            await PlatformAudit.LogAsync(audit, currentUser, "RemoveAnnouncement", announcement.TenantId,
                "Announcement", id.ToString(), new { announcement.Title }, ct);
            return Results.Ok(new { isSuccess = true });
        });
    }

    /// <summary>Copies the body onto the announcement, or says what is wrong with it.</summary>
    private static string? Apply(Announcement a, AnnouncementBody body)
    {
        var title = body.Title?.Trim() ?? "";
        var text = body.Body?.Trim() ?? "";
        if (title.Length == 0) return "Give the announcement a title.";
        if (title.Length > 200) return "Keep the title under 200 characters.";
        if (text.Length == 0) return "Write the message.";
        if (text.Length > 4000) return "Keep the message under 4,000 characters.";

        var level = AnnouncementLevel.Info;
        if (!string.IsNullOrWhiteSpace(body.Level) && !Enum.TryParse(body.Level, ignoreCase: true, out level))
            return "Level is Info, Warning or Critical.";

        var starts = body.StartsAt is DateTime s ? DateTime.SpecifyKind(s.ToUniversalTime(), DateTimeKind.Utc) : DateTime.UtcNow;
        DateTime? ends = body.EndsAt is DateTime e ? DateTime.SpecifyKind(e.ToUniversalTime(), DateTimeKind.Utc) : null;
        if (ends is not null && ends <= starts) return "The end has to be after the start.";

        a.Title = title;
        a.Body = text;
        a.Level = level;
        a.TenantId = body.TenantId;
        a.StartsAt = starts;
        a.EndsAt = ends;
        return null;
    }
}
