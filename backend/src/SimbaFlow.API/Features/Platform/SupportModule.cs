using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Platform;

namespace SimbaFlow.API.Features.Platform;

public record RaiseSupportBody(string Subject, string Message);

public record SupportReplyBody(string Message);

/// <summary>
/// Help requests from agencies to the platform team.
///
/// Agency side (<c>/api/support</c>): anyone in an agency can raise a request and see and add to
/// their agency's. Platform side (<c>/api/platform/support</c>): the whole platform team works
/// one inbox across every agency.
/// </summary>
public class SupportModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ──── Agency side ────
        var agency = app.MapGroup("/api/support")
            .WithTags("Support")
            .RequireAuthorization();

        agency.MapGet("/", async (IPlatformDbContext db, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            if (currentUser.TenantId is not Guid tenantId)
                return NoAgency();
            var data = await db.SupportTickets.AsNoTracking()
                .Where(t => t.TenantId == tenantId && !t.IsDeleted)
                .OrderByDescending(t => t.LastActivityAt)
                .Take(200)
                .Select(t => new
                {
                    t.Id, t.Subject, Status = t.Status.ToString(), t.CreatedAt, t.LastActivityAt,
                    Messages = t.Messages.Count,
                })
                .ToListAsync(ct);
            return Results.Ok(new { isSuccess = true, data });
        });

        agency.MapPost("/", async (
            RaiseSupportBody body, IPlatformDbContext db, IAuditService audit,
            ICurrentUserService currentUser, CancellationToken ct) =>
        {
            if (currentUser.TenantId is not Guid tenantId || !Guid.TryParse(currentUser.UserId, out var userId))
                return NoAgency();
            if ((Refusal(body.Subject, 200, "a subject") ?? Refusal(body.Message, 8000, "what you need help with")) is { } error)
                return Results.Json(new { isSuccess = false, error }, statusCode: 400);

            var now = DateTime.UtcNow;
            var ticket = new SupportTicket
            {
                TenantId = tenantId,
                Subject = body.Subject.Trim(),
                RaisedByUserId = userId,
                LastActivityAt = now,
                Messages = [new SupportMessage { AuthorUserId = userId, Body = body.Message.Trim(), FromPlatform = false }],
            };
            db.SupportTickets.Add(ticket);
            await db.SaveChangesAsync(ct);
            await PlatformAudit.LogAsync(audit, currentUser, "RaiseSupportRequest", tenantId, "SupportTicket",
                ticket.Id.ToString(), new { ticket.Subject }, ct);
            return Results.Ok(new { isSuccess = true, data = new { ticket.Id } });
        });

        agency.MapGet("/{id:guid}", async (
            Guid id, IPlatformDbContext db, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            if (currentUser.TenantId is not Guid tenantId)
                return NoAgency();
            return await TicketAsync(db, id, tenantId, ct);
        });

        agency.MapPost("/{id:guid}/reply", async (
            Guid id, SupportReplyBody body, IPlatformDbContext db, IAuditService audit,
            ICurrentUserService currentUser, CancellationToken ct) =>
        {
            if (currentUser.TenantId is not Guid tenantId)
                return NoAgency();
            return await ReplyAsync(db, audit, currentUser, id, tenantId, body, fromPlatform: false, ct);
        });

        agency.MapPost("/{id:guid}/close", async (
            Guid id, IPlatformDbContext db, IAuditService audit, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            if (currentUser.TenantId is not Guid tenantId)
                return NoAgency();
            return await CloseAsync(db, audit, currentUser, id, tenantId, ct);
        });

        // ──── Platform side ────
        var staff = app.MapGroup("/api/platform/support")
            .WithTags("Support")
            .RequireAuthorization("PlatformStaff");

        staff.MapGet("/", async (IPlatformDbContext db, string? status, CancellationToken ct) =>
        {
            var q = db.SupportTickets.AsNoTracking().Where(t => !t.IsDeleted);
            if (Enum.TryParse<SupportTicketStatus>(status, ignoreCase: true, out var s))
                q = q.Where(t => t.Status == s);

            var tickets = await q
                .OrderBy(t => t.Status).ThenByDescending(t => t.LastActivityAt)
                .Take(300)
                .Select(t => new
                {
                    t.Id, t.TenantId, t.Subject, t.Status, t.CreatedAt, t.LastActivityAt, t.RaisedByUserId,
                    Messages = t.Messages.Count,
                })
                .ToListAsync(ct);

            var tenantIds = tickets.Select(t => t.TenantId).Distinct().ToList();
            var agencies = await db.Tenants.AsNoTracking()
                .Where(t => tenantIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, ct);
            var userIds = tickets.Select(t => t.RaisedByUserId).Distinct().ToList();
            var people = await Names(db, userIds, ct);

            var data = tickets.Select(t => new
            {
                t.Id, t.TenantId, Agency = agencies.GetValueOrDefault(t.TenantId) ?? "Unknown agency",
                t.Subject, Status = t.Status.ToString(), t.CreatedAt, t.LastActivityAt,
                RaisedBy = people.GetValueOrDefault(t.RaisedByUserId) ?? "Unknown user",
                t.Messages,
            });
            return Results.Ok(new { isSuccess = true, data });
        });

        staff.MapGet("/{id:guid}", async (Guid id, IPlatformDbContext db, CancellationToken ct) =>
            await TicketAsync(db, id, tenantId: null, ct));

        staff.MapPost("/{id:guid}/reply", async (
            Guid id, SupportReplyBody body, IPlatformDbContext db, IAuditService audit,
            ICurrentUserService currentUser, CancellationToken ct) =>
            await ReplyAsync(db, audit, currentUser, id, tenantId: null, body, fromPlatform: true, ct));

        staff.MapPost("/{id:guid}/close", async (
            Guid id, IPlatformDbContext db, IAuditService audit, ICurrentUserService currentUser, CancellationToken ct) =>
            await CloseAsync(db, audit, currentUser, id, tenantId: null, ct));
    }

    private static IResult NoAgency() =>
        Results.Json(new { isSuccess = false, error = "Help requests are raised from inside an agency." }, statusCode: 400);

    private static string? Refusal(string? text, int max, string what)
    {
        var t = text?.Trim() ?? "";
        if (t.Length == 0) return $"Write {what}.";
        if (t.Length > max) return $"Keep it under {max:N0} characters.";
        return null;
    }

    private static async Task<Dictionary<Guid, string>> Names(IPlatformDbContext db, List<Guid> ids, CancellationToken ct) =>
        (await db.ApplicationUsers.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(ct))
        .ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } n ? n : u.Email ?? "Unknown user");

    /// <summary>One ticket with its messages. <paramref name="tenantId"/> limits it to one agency.</summary>
    private static async Task<IResult> TicketAsync(IPlatformDbContext db, Guid id, Guid? tenantId, CancellationToken ct)
    {
        var ticket = await db.SupportTickets.AsNoTracking()
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted && (tenantId == null || t.TenantId == tenantId), ct);
        if (ticket is null)
            return Results.Json(new { isSuccess = false, error = "Request not found." }, statusCode: 404);

        var people = await Names(db, ticket.Messages.Select(m => m.AuthorUserId).Distinct().ToList(), ct);
        var agency = await db.Tenants.AsNoTracking().Where(t => t.Id == ticket.TenantId).Select(t => t.Name).FirstOrDefaultAsync(ct);

        return Results.Ok(new
        {
            isSuccess = true,
            data = new
            {
                ticket.Id, ticket.TenantId, Agency = agency, ticket.Subject, Status = ticket.Status.ToString(),
                ticket.CreatedAt, ticket.LastActivityAt,
                Messages = ticket.Messages.OrderBy(m => m.CreatedAt).Select(m => new
                {
                    m.Id, m.Body, m.FromPlatform, m.CreatedAt,
                    Author = m.FromPlatform ? $"{people.GetValueOrDefault(m.AuthorUserId) ?? "Platform team"} (SimbaFlow)"
                        : people.GetValueOrDefault(m.AuthorUserId) ?? "Unknown user",
                }),
            },
        });
    }

    private static async Task<IResult> ReplyAsync(
        IPlatformDbContext db, IAuditService audit, ICurrentUserService currentUser,
        Guid id, Guid? tenantId, SupportReplyBody body, bool fromPlatform, CancellationToken ct)
    {
        if (!Guid.TryParse(currentUser.UserId, out var userId))
            return Results.Unauthorized();
        if (Refusal(body.Message, 8000, "a reply") is { } error)
            return Results.Json(new { isSuccess = false, error }, statusCode: 400);

        var ticket = await db.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted && (tenantId == null || t.TenantId == tenantId), ct);
        if (ticket is null)
            return Results.Json(new { isSuccess = false, error = "Request not found." }, statusCode: 404);

        db.SupportMessages.Add(new SupportMessage
        {
            TicketId = ticket.Id, AuthorUserId = userId, Body = body.Message.Trim(), FromPlatform = fromPlatform,
        });
        // A reply from the agency reopens it for the platform; one from the platform waits on them.
        ticket.Status = fromPlatform ? SupportTicketStatus.Answered : SupportTicketStatus.Open;
        ticket.LastActivityAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await PlatformAudit.LogAsync(audit, currentUser, "ReplySupportRequest", ticket.TenantId, "SupportTicket",
            ticket.Id.ToString(), ct: ct);
        return Results.Ok(new { isSuccess = true });
    }

    private static async Task<IResult> CloseAsync(
        IPlatformDbContext db, IAuditService audit, ICurrentUserService currentUser,
        Guid id, Guid? tenantId, CancellationToken ct)
    {
        var ticket = await db.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted && (tenantId == null || t.TenantId == tenantId), ct);
        if (ticket is null)
            return Results.Json(new { isSuccess = false, error = "Request not found." }, statusCode: 404);

        ticket.Status = SupportTicketStatus.Closed;
        ticket.LastActivityAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await PlatformAudit.LogAsync(audit, currentUser, "CloseSupportRequest", ticket.TenantId, "SupportTicket",
            ticket.Id.ToString(), ct: ct);
        return Results.Ok(new { isSuccess = true });
    }
}
