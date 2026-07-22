using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Offices;

public class OfficeModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/offices")
            .WithTags("Offices")
            .RequireAuthorization();

        group.MapGet("/", async (IApplicationDbContext db) =>
        {
            var offices = await db.Offices.AsNoTracking()
                .Where(o => !o.IsDeleted && o.IsActive)
                .OrderBy(o => o.SortOrder)
                .Select(o => new
                {
                    o.Id,
                    o.Name,
                    o.Code,
                    o.City,
                    o.Phone,
                    o.Email,
                    o.IsActive
                })
                .ToListAsync();

            return Results.Ok(Result<object>.Success(offices));
        });
    }
}
