using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence.Seeds;

namespace SimbaFlow.API.Features.Tenants;

/// <summary>
/// Body of the intake-defaults save. A class rather than a positional record: the optional
/// CvTemplate trailing parameter made System.Text.Json skip sibling properties on some payloads,
/// so gender and occupation arrived null while the layout still bound.
/// </summary>
public sealed class IntakeDefaultsBody
{
    public string? Gender { get; set; }
    public string? Occupation { get; set; }
    public string? Religion { get; set; }
    public string? Nationality { get; set; }
    public string? PassportType { get; set; }
    public string? MaritalStatus { get; set; }
    public string? CountryOfTravel { get; set; }
    public string? ContractPeriod { get; set; }
    public string? CvTemplate { get; set; }
    public string? CookingLevel { get; set; }
    public List<IntakeSkillBody>? Skills { get; set; }
}

public sealed class IntakeSkillBody
{
    public Guid? Id { get; set; }
    public string? Name { get; set; }
    public bool IsDefaultSelected { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// The values a blank candidate form starts with, per agency.
/// Stored as rows in the tenant schema, not as JSON on TenantInfo.
/// </summary>
public class IntakeDefaultsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/intake-defaults").RequireAuthorization();

        group.MapGet("/", async (
            ITenantDbContext tenantDb,
            IPlatformDbContext platform,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            var loaded = await LoadOrSeedAsync(tenantDb, platform, user, ct);
            if (loaded is null)
                return Results.Ok(new { isSuccess = true, data = ToDto(AgencyIntakeSeeder.FromLegacy(null), BuiltInFallback()) });

            return Results.Ok(new { isSuccess = true, data = ToDto(loaded.Value.Defaults, loaded.Value.Skills) });
        });

        // What a layout looks like, drawn with stand-in details and this agency's own letterhead.
        // Inline rather than an attachment: this is meant to be looked at, not filed.
        group.MapGet("/cv-preview/{template}", async (
            string template,
            Application.Common.Interfaces.ICvGenerationService cv,
            CancellationToken ct) =>
        {
            var pdf = await cv.GeneratePreviewAsync(template, ct);
            return Results.File(pdf, "application/pdf", enableRangeProcessing: false);
        });

        // The same thing as a picture, which is what the picker shows.
        group.MapGet("/cv-preview/{template}/image", async (
            string template,
            Application.Common.Interfaces.ICvGenerationService cv,
            CancellationToken ct) =>
        {
            var png = await cv.GeneratePreviewImageAsync(template, ct);
            return Results.File(png, "image/png", enableRangeProcessing: false);
        });

        group.MapPut("/", async (
            IntakeDefaultsBody body,
            ITenantDbContext tenantDb,
            IPlatformDbContext platform,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (!user.HasPermission("settings.write") && !user.IsSuperAdmin)
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var loaded = await LoadOrSeedAsync(tenantDb, platform, user, ct);
            if (loaded is null)
                return Results.Json(new { isSuccess = false, error = "No agency on this account." }, statusCode: 400);

            Apply(loaded.Value.Defaults, body);
            if (body.Skills is not null)
                ApplySkills(tenantDb, loaded.Value.Skills, body.Skills);

            await tenantDb.SaveChangesAsync(ct);

            var skills = await tenantDb.AgencySkills
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Name)
                .ToListAsync(ct);

            return Results.Ok(new { isSuccess = true, data = ToDto(loaded.Value.Defaults, skills) });
        });
    }

    /// <summary>
    /// Merge a save payload onto the agency's current defaults. Empty field values are kept:
    /// "No default — ask each time" is a real answer, not a request to restore the built-in
    /// starting points.
    /// </summary>
    public static void Apply(AgencyIntakeDefaults current, IntakeDefaultsBody body)
    {
        current ??= new AgencyIntakeDefaults();
        body ??= new IntakeDefaultsBody();
        current.Gender = body.Gender is "0" or "1" ? body.Gender : "";
        current.Occupation = (body.Occupation ?? "").Trim();
        current.Religion = (body.Religion ?? "").Trim();
        current.Nationality = (body.Nationality ?? "").Trim();
        current.PassportType = (body.PassportType ?? "").Trim();
        current.MaritalStatus = (body.MaritalStatus ?? "").Trim();
        current.CountryOfTravel = (body.CountryOfTravel ?? "").Trim();
        current.ContractPeriod = (body.ContractPeriod ?? "").Trim();
        current.CookingLevel = (body.CookingLevel ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(body.CvTemplate))
            current.CvTemplate = CvTemplates.Normalise(body.CvTemplate);
        else
            current.CvTemplate = CvTemplates.Normalise(current.CvTemplate);
    }

    /// <summary>Kept so tests that still construct TenantSettings can pin the JSON backfill shape.</summary>
    public static TenantSettings Apply(TenantSettings current, IntakeDefaultsBody body)
    {
        current ??= new TenantSettings();
        var row = AgencyIntakeSeeder.FromLegacy(current);
        Apply(row, body);
        return new TenantSettings
        {
            DefaultLanguage = current.DefaultLanguage,
            SupportedLanguages = current.SupportedLanguages,
            DefaultCurrency = current.DefaultCurrency,
            SupportedCurrencies = current.SupportedCurrencies,
            MaxFileUploadSizeMB = current.MaxFileUploadSizeMB,
            SignalREnabled = current.SignalREnabled,
            BotEnabled = current.BotEnabled,
            Documents = new DocumentSettings { CvTemplate = row.CvTemplate },
            Intake = new IntakeDefaults
            {
                Gender = row.Gender,
                Occupation = row.Occupation,
                Religion = row.Religion,
                Nationality = row.Nationality,
                PassportType = row.PassportType,
                MaritalStatus = row.MaritalStatus,
                CountryOfTravel = row.CountryOfTravel,
                ContractPeriod = row.ContractPeriod,
            },
        };
    }

    public static void ApplySkills(
        ITenantDbContext db,
        IReadOnlyList<AgencySkill> existing,
        IReadOnlyList<IntakeSkillBody> payload)
    {
        var live = existing.Where(s => !s.IsDeleted).ToList();
        var seen = new HashSet<Guid>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in payload.OrderBy(s => s.SortOrder))
        {
            var name = (item.Name ?? "").Trim();
            if (name.Length == 0)
                continue;
            if (name.Length > 128)
                name = name[..128];

            AgencySkill? row = null;
            if (item.Id is Guid id)
                row = live.FirstOrDefault(s => s.Id == id);

            if (row is null)
            {
                if (usedNames.Contains(name) || live.Any(s => !s.IsDeleted && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (BuiltInSkills.IsBuiltInName(name))
                    continue;

                row = new AgencySkill
                {
                    Name = name,
                    IsBuiltIn = false,
                    BuiltInKey = null,
                    IsDefaultSelected = item.IsDefaultSelected,
                    SortOrder = item.SortOrder,
                };
                db.AgencySkills.Add(row);
                live.Add(row);
            }
            else
            {
                if (!row.IsBuiltIn)
                    row.Name = name;
                row.IsDefaultSelected = item.IsDefaultSelected;
                row.SortOrder = item.SortOrder;
            }

            seen.Add(row.Id);
            usedNames.Add(row.Name);
        }

        foreach (var row in live.Where(s => !s.IsBuiltIn && !seen.Contains(s.Id)))
            row.IsDeleted = true;
    }

    /// <summary>
    /// Explicit camelCase names so the settings page and the new-candidate form read the same
    /// object even if HTTP JSON naming is not applied to inferred anonymous-type properties.
    /// </summary>
    public static object ToDto(AgencyIntakeDefaults defaults, IEnumerable<AgencySkill> skills)
    {
        defaults ??= new AgencyIntakeDefaults();
        return new
        {
            gender = defaults.Gender,
            occupation = defaults.Occupation,
            religion = defaults.Religion,
            nationality = defaults.Nationality,
            passportType = defaults.PassportType,
            maritalStatus = defaults.MaritalStatus,
            countryOfTravel = defaults.CountryOfTravel,
            contractPeriod = defaults.ContractPeriod,
            cookingLevel = defaults.CookingLevel,
            cvTemplate = CvTemplates.Normalise(defaults.CvTemplate),
            cvTemplates = CvTemplates.All
                .Select(x => new { value = x.Value, name = x.Name, description = x.Description }),
            skills = skills
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Name)
                .Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    builtInKey = s.BuiltInKey,
                    isBuiltIn = s.IsBuiltIn,
                    isDefaultSelected = s.IsDefaultSelected,
                    sortOrder = s.SortOrder,
                }),
        };
    }

    public static object ToDto(TenantSettings settings) =>
        ToDto(AgencyIntakeSeeder.FromLegacy(settings), BuiltInFallback());

    private static List<AgencySkill> BuiltInFallback() =>
        BuiltInSkills.All.Select(s => new AgencySkill
        {
            Name = s.Name,
            BuiltInKey = s.Key,
            IsBuiltIn = true,
            IsDefaultSelected = s.DefaultSelected,
            SortOrder = s.SortOrder,
        }).ToList();

    private static async Task<(AgencyIntakeDefaults Defaults, List<AgencySkill> Skills)?> LoadOrSeedAsync(
        ITenantDbContext tenantDb,
        IPlatformDbContext platform,
        ICurrentUserService user,
        CancellationToken ct)
    {
        if (user.TenantId is not Guid tenantId)
            return null;

        var legacy = await platform.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId && !t.IsDeleted)
            .Select(t => t.Settings)
            .FirstOrDefaultAsync(ct);

        await AgencyIntakeSeeder.EnsureAsync(tenantDb, legacy, ct);

        var defaults = await tenantDb.AgencyIntakeDefaults
            .FirstOrDefaultAsync(x => !x.IsDeleted, ct);
        if (defaults is null)
            return null;

        var skills = await tenantDb.AgencySkills
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

        return (defaults, skills);
    }
}
