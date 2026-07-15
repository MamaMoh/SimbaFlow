using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimbaFlow.Domain.Entities.Locations;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds default office/location hierarchy for the labour export agency.
/// </summary>
public static class LocationSeeder
{
    public static async Task SeedLocationsAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        if (await context.Locations.AnyAsync())
            return;

        // Headquarters
        var hq = new Location
        {
            Code = "HQ-ADD",
            Name = "Addis Ababa Head Office",
            Type = LocationType.Headquarters,
            Description = "Main agency headquarters in Addis Ababa, Ethiopia",
            SortOrder = 1
        };
        context.Locations.Add(hq);

        // Branch offices
        context.Locations.AddRange(
            new Location { Code = "BR-DIRE", Name = "Dire Dawa Branch", Type = LocationType.BranchOffice, ParentLocationId = hq.Id, Description = "Dire Dawa, Ethiopia", SortOrder = 2 },
            new Location { Code = "BR-ADAMA", Name = "Adama Branch", Type = LocationType.BranchOffice, ParentLocationId = hq.Id, Description = "Adama, Ethiopia", SortOrder = 3 }
        );

        // Overseas offices
        context.Locations.AddRange(
            new Location { Code = "OV-RIYADH", Name = "Riyadh Office", Type = LocationType.OverseasOffice, Description = "Riyadh, Saudi Arabia", SortOrder = 10 },
            new Location { Code = "OV-DUBAI", Name = "Dubai Office", Type = LocationType.OverseasOffice, Description = "Dubai, UAE", SortOrder = 11 },
            new Location { Code = "OV-KUWAIT", Name = "Kuwait Office", Type = LocationType.OverseasOffice, Description = "Kuwait City, Kuwait", SortOrder = 12 }
        );

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded office/location hierarchy ({Count} locations)",
            await context.Locations.CountAsync());
    }
}
