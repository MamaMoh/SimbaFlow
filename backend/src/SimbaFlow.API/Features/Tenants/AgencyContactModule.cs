using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Features.Tenants;

/// <summary>
/// The agency's own contact details, read by the agency itself.
///
/// All of this is already on TenantInfo and already used — the contract prints ContactEmail as
/// the Ethiopian party's address. It was simply never readable by anybody inside the agency:
/// /api/tenants is SuperAdmin-only, because it is the platform's view of every agency.
///
/// Wanted by the visa application, which asks for an email and means the office's, not the
/// candidate's. Filling that from the candidate's record would put a housemaid's gmail address
/// on a consular application as the agency's contact.
/// </summary>
public class AgencyContactModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings/agency-contact", async (
            IPlatformDbContext platform,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (user.TenantId is not Guid tenantId)
                return Results.Json(
                    new { isSuccess = false, error = "This account is not inside an agency" },
                    statusCode: 400);

            var agency = await platform.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId && !t.IsDeleted)
                .Select(t => new
                {
                    t.Name,
                    t.ContactEmail,
                    t.ContactPhone,
                    t.Address,
                    t.City,
                    t.Country,
                    t.LicenseNumber
                })
                .FirstOrDefaultAsync(ct);

            if (agency is null)
                return Results.Json(
                    new { isSuccess = false, error = "Agency not found" },
                    statusCode: 404);

            return Results.Ok(new { isSuccess = true, data = agency });
        })
        .WithTags("Tenants")
        .RequireAuthorization();
    }
}
