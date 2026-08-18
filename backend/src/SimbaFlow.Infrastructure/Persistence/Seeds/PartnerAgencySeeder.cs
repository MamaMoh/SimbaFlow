using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimbaFlow.Domain.Entities.Partners;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds a starter foreign partner agency catalog (MoLS Art. 40 capacity tiers).
/// Idempotent by name + country code.
/// </summary>
public static class PartnerAgencySeeder
{
    private static readonly (string Name, string Code, string Country, string? License)[] SeedData =
    [
        ("Etenaa Resources Co.", "SA", "Saudi Arabia", "SA-MOL-48291"),
        ("Al Nour Manpower LLC", "AE", "United Arab Emirates", "UAE-MOHRE-11902"),
        ("Kuwait Home Care Agency", "KW", "Kuwait", "KW-PAM-3301"),
        ("Qatar Domestic Services", "QA", "Qatar", "QA-ADLSA-7740"),
        ("Bahrain Staffing Partners", "BH", "Bahrain", "BH-LMRA-5512"),
        ("Jordan Care Recruitment", "JO", "Jordan", "JO-MOL-2208"),
        ("Oman Gulf Manpower", "OM", "Oman", "OM-MOL-981"),
        ("Riyadh Premier Domestic", "SA", "Saudi Arabia", "SA-MOL-51002"),
        ("Dubai Household Services", "AE", "United Arab Emirates", "UAE-MOHRE-22011"),
        ("Lebanon Domestic Link", "LB", "Lebanon", "LB-MOL-441"),
    ];

    public static async Task SeedPartnerAgenciesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PlatformDbContext>>();

        var existing = await context.PartnerAgencies
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Select(p => new { p.Name, p.CountryCode })
            .ToListAsync();

        var existingKeys = existing
            .Select(p => $"{p.CountryCode}|{p.Name}".ToUpperInvariant())
            .ToHashSet();

        var added = 0;
        foreach (var (name, code, country, license) in SeedData)
        {
            var key = $"{code}|{name}".ToUpperInvariant();
            if (existingKeys.Contains(key)) continue;

            context.PartnerAgencies.Add(new PartnerAgency
            {
                Name = name,
                CountryCode = code,
                CountryName = country,
                ForeignLicenseId = license,
                IsActive = true,
                ContactEmail = $"ops@{code.ToLowerInvariant()}.partner.example"
            });
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} partner agencies into catalog", added);
        }
    }
}
