using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Features.Diagnostics;

/// <summary>
/// Error visibility. Platform-admin only: stack traces and paths across every tenant.
/// </summary>
public class DiagnosticsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/diagnostics").RequireAuthorization();

        // Grouped by fingerprint so one recurring fault is one row with a count, not a thousand.
        group.MapGet("/errors", async (
            bool? includeResolved,
            IPlatformDbContext context,
            ICurrentUserService user) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("system.admin"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var q = context.ErrorEvents.AsNoTracking().Where(e => !e.IsDeleted);
            if (includeResolved != true)
                q = q.Where(e => e.ResolvedAt == null);

            var groups = await q
                .GroupBy(e => e.Fingerprint)
                .Select(g => new
                {
                    Fingerprint = g.Key,
                    Count = g.Count(),
                    LastSeen = g.Max(x => x.OccurredAt),
                    FirstSeen = g.Min(x => x.OccurredAt),
                    ExceptionType = g.Max(x => x.ExceptionType),
                    Message = g.Max(x => x.Message),
                    Path = g.Max(x => x.Path),
                    Source = g.Max(x => x.Source),
                    AffectedUsers = g.Select(x => x.UserId).Distinct().Count(),
                })
                .OrderByDescending(x => x.LastSeen)
                .Take(200)
                .ToListAsync();

            return Results.Ok(new { isSuccess = true, data = groups });
        });

        group.MapPost("/errors/{fingerprint}/resolve", async (
            string fingerprint,
            IPlatformDbContext context,
            ICurrentUserService user) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("system.admin"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var rows = await context.ErrorEvents
                .Where(e => e.Fingerprint == fingerprint && e.ResolvedAt == null && !e.IsDeleted)
                .ToListAsync();

            foreach (var r in rows)
            {
                r.ResolvedAt = DateTime.UtcNow;
                r.ResolvedBy = user.UserName;
            }
            await context.SaveChangesAsync();

            return Results.Ok(new { isSuccess = true, data = rows.Count });
        });

        // The browser reports its own crashes here. Authenticated so it cannot be used
        // anonymously to fill the table, and rate limited for the same reason.
        group.MapPost("/client-error", async (
            ClientErrorReport body,
            IErrorTracker tracker) =>
        {
            if (string.IsNullOrWhiteSpace(body.Message))
                return Results.Json(new { isSuccess = false, error = "Message is required" }, statusCode: 400);

            await tracker.CaptureClientAsync(body.Message, body.Stack, body.Path);
            return Results.Ok(new { isSuccess = true });
        }).RequireRateLimiting("general");
    }
}

public record ClientErrorReport(string Message, string? Stack, string? Path);
