using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Audit.Queries;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Features.Platform;

/// <summary>
/// The audit trail across every agency, for the platform team: "who suspended this agency",
/// "what did support do this week". Changes only; what people looked at stays on each agency's
/// own page.
///
/// <c>scope=platform</c> narrows it to actions taken by the platform team (accounts with no
/// agency) and by the system itself.
/// </summary>
public class PlatformAuditModule : ICarterModule
{
    private const int MaxPageSize = 100;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/platform/audit", async (
            IPlatformDbContext db,
            SimbaFlow.Infrastructure.Persistence.PlatformDbContext platform,
            Guid? tenantId,
            string? userId,
            string? action,
            string? scope,
            DateOnly? from,
            DateOnly? to,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var staffIds = await db.ApplicationUsers.AsNoTracking()
                .Where(u => u.TenantId == null)
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync(ct);
            var staffIdStrings = staffIds.Select(u => u.Id.ToString()).ToList();

            var q = platform.AuditLogs.AsNoTracking().AsQueryable();
            if (tenantId is Guid t)
            {
                var asText = t.ToString();
                q = q.Where(a => a.TenantId == t || (a.EntityType == "Tenant" && a.EntityId == asText));
            }
            if (string.Equals(scope, "platform", StringComparison.OrdinalIgnoreCase))
                q = q.Where(a => a.UserId == null || staffIdStrings.Contains(a.UserId));
            if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(a => a.UserId == userId);
            if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
            if (from is DateOnly f)
            {
                var start = f.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                q = q.Where(a => a.Timestamp >= start);
            }
            if (to is DateOnly e)
            {
                var end = e.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                q = q.Where(a => a.Timestamp < end);
            }

            var total = await q.CountAsync(ct);
            var rows = await q.OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(ct);

            var authorIds = rows.Select(r => Guid.TryParse(r.UserId, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty).Distinct().ToList();
            var authors = await db.ApplicationUsers.AsNoTracking()
                .Where(u => authorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.TenantId })
                .ToDictionaryAsync(u => u.Id.ToString(), ct);
            var agencies = await db.Tenants.AsNoTracking()
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

            var items = rows.Select(r =>
            {
                authors.TryGetValue(r.UserId ?? "", out var author);
                Guid? agencyId = r.TenantId
                    ?? (r.EntityType == "Tenant" && Guid.TryParse(r.EntityId, out var eid) ? eid : null);
                return new
                {
                    r.Id,
                    At = DateTime.SpecifyKind(r.Timestamp, DateTimeKind.Utc),
                    r.UserId,
                    UserName = author is null
                        ? (r.UserId is null ? "System" : "Unknown user")
                        : ($"{author.FirstName} {author.LastName}".Trim() is { Length: > 0 } n ? n : author.Email ?? "Unknown user"),
                    IsPlatformTeam = author is not null && author.TenantId is null,
                    Action = AuditActionNames.Describe(r.Action),
                    RawAction = r.Action,
                    AgencyId = agencyId,
                    Agency = agencyId is Guid a ? agencies.GetValueOrDefault(a) : null,
                    r.EntityType,
                    r.EntityId,
                    Details = r.NewValues,
                    r.Success,
                    r.IpAddress,
                };
            }).ToList();

            var users = staffIds
                .Select(u => new { value = u.Id.ToString(), label = $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } n ? n : u.Email ?? "" })
                .OrderBy(u => u.label)
                .ToList();
            var agencyOptions = agencies
                .Select(x => new { value = x.Key.ToString(), label = x.Value })
                .OrderBy(x => x.label)
                .ToList();

            return Results.Ok(new
            {
                isSuccess = true,
                data = new { items, total, page, pageSize, users, agencies = agencyOptions },
            });
        }).WithTags("Audit").RequireAuthorization("PlatformStaff");
    }
}
