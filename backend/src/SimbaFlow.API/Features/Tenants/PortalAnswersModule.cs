using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Tenancy;

namespace SimbaFlow.API.Features.Tenants;

public sealed class PortalAnswerBody
{
    public string? SiteId { get; set; }
    public string? Label { get; set; }
    public string? Value { get; set; }
}

public sealed class PortalAnswersBody
{
    public List<PortalAnswerBody>? Answers { get; set; }
}

/// <summary>
/// The boxes on other people's forms that this agency always answers the same way.
///
/// Read by anyone who can see settings, because filling a form needs them; written only by
/// someone who can change settings. The form maps ship a default for every one of these, so an
/// agency that never opens this screen still files correct applications — a row here is an
/// agency saying otherwise.
/// </summary>
public class PortalAnswersModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/portal-answers")
            .WithTags("Tenants")
            .RequireAuthorization();

        group.MapGet("/", async (ITenantDbContext db, CancellationToken ct) =>
        {
            var rows = await db.AgencyPortalAnswers
                .AsNoTracking()
                .Where(a => !a.IsDeleted)
                .Select(a => new { a.SiteId, a.Label, a.Value })
                .ToListAsync(ct);

            return Results.Ok(new { isSuccess = true, data = rows });
        });

        group.MapPut("/", async (
            PortalAnswersBody body,
            ITenantDbContext db,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (!user.HasPermission("settings.write") && !user.IsSuperAdmin)
                return Results.Json(
                    new { isSuccess = false, error = "You cannot change the agency's settings" },
                    statusCode: 403);

            var incoming = body.Answers ?? [];
            if (incoming.Count > 200)
                return Results.Json(
                    new { isSuccess = false, error = "Too many answers in one save" },
                    statusCode: 400);

            var existing = await db.AgencyPortalAnswers
                .Where(a => !a.IsDeleted)
                .ToListAsync(ct);

            foreach (var answer in incoming)
            {
                var siteId = (answer.SiteId ?? string.Empty).Trim();
                var label = (answer.Label ?? string.Empty).Trim();
                if (siteId.Length == 0 || label.Length == 0) continue;
                if (siteId.Length > 64 || label.Length > 128) continue;

                var value = (answer.Value ?? string.Empty).Trim();
                if (value.Length > 256) value = value[..256];

                var row = existing.FirstOrDefault(a => a.SiteId == siteId && a.Label == label);

                // Cleared means "go back to what the map ships", which is a deletion rather than
                // an empty answer — an empty answer would blank the box on every application.
                if (value.Length == 0)
                {
                    if (row is not null) row.IsDeleted = true;
                    continue;
                }

                if (row is null)
                {
                    db.AgencyPortalAnswers.Add(new AgencyPortalAnswer
                    {
                        SiteId = siteId,
                        Label = label,
                        Value = value
                    });
                }
                else
                {
                    row.Value = value;
                }
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { isSuccess = true });
        });
    }
}
