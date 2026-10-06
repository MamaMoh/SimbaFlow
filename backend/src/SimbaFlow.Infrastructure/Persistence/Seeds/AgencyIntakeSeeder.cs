using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.Infrastructure.Persistence.Seeds;

/// <summary>
/// Creates the singleton intake-defaults row and the built-in skill catalog when a tenant
/// schema is new, or when the table is empty after upgrading from TenantSettings JSON.
/// </summary>
public static class AgencyIntakeSeeder
{
    public static async Task EnsureAsync(
        ITenantDbContext db,
        TenantSettings? legacy,
        CancellationToken cancellationToken = default)
    {
        var defaults = await db.AgencyIntakeDefaults
            .FirstOrDefaultAsync(x => !x.IsDeleted, cancellationToken);

        if (defaults is null)
        {
            db.AgencyIntakeDefaults.Add(FromLegacy(legacy));
        }

        var existing = await db.AgencySkills
            .Where(s => !s.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var spec in BuiltInSkills.All)
        {
            if (existing.Any(s => s.BuiltInKey == spec.Key))
                continue;

            db.AgencySkills.Add(new AgencySkill
            {
                Name = spec.Name,
                BuiltInKey = spec.Key,
                IsBuiltIn = true,
                IsDefaultSelected = spec.DefaultSelected,
                SortOrder = spec.SortOrder,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static AgencyIntakeDefaults FromLegacy(TenantSettings? settings)
    {
        var intake = settings?.Intake ?? new IntakeDefaults();
        return new AgencyIntakeDefaults
        {
            SingletonKey = 1,
            Gender = intake.Gender,
            Occupation = intake.Occupation,
            Religion = intake.Religion,
            Nationality = intake.Nationality,
            PassportType = intake.PassportType,
            MaritalStatus = intake.MaritalStatus,
            CountryOfTravel = intake.CountryOfTravel,
            ContractPeriod = intake.ContractPeriod,
            CvTemplate = CvTemplates.Normalise(settings?.Documents.CvTemplate),
            CookingLevel = "",
        };
    }
}
