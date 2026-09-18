using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Services;

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
}

/// <summary>
/// The values a blank candidate form starts with, per agency.
/// </summary>
public class IntakeDefaultsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/intake-defaults").RequireAuthorization();

        group.MapGet("/", async (IPlatformDbContext db, ICurrentUserService user) =>
        {
            var tenant = await LoadAsync(db, user);
            // A blank form is not an error: an agency that has set nothing gets the built-in values.
            var settings = tenant?.Settings ?? new TenantSettings();
            return Results.Ok(new { isSuccess = true, data = ToDto(settings) });
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
            IntakeDefaultsBody body, IPlatformDbContext db, ICurrentUserService user) =>
        {
            if (!user.HasPermission("settings.write") && !user.IsSuperAdmin)
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var tenant = await LoadAsync(db, user);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "No agency on this account." }, statusCode: 400);

            tenant.Settings = Apply(tenant.Settings, body);

            // Settings is a JSON converted property. IPlatformDbContext does not expose Entry, so
            // mark the column dirty through the concrete context — otherwise SaveChanges can
            // return success without writing a row.
            if (db is DbContext ctx)
                ctx.Entry(tenant).Property(t => t.Settings).IsModified = true;

            await db.SaveChangesAsync(default);
            return Results.Ok(new { isSuccess = true, data = ToDto(tenant.Settings) });
        });
    }

    /// <summary>
    /// Merge a save payload onto the agency's current settings. Empty field values are kept:
    /// "No default — ask each time" is a real answer, not a request to restore the built-in
    /// starting points.
    /// </summary>
    public static TenantSettings Apply(TenantSettings current, IntakeDefaultsBody body)
    {
        current ??= new TenantSettings();
        body ??= new IntakeDefaultsBody();
        return new TenantSettings
        {
            DefaultLanguage = current.DefaultLanguage,
            SupportedLanguages = current.SupportedLanguages,
            DefaultCurrency = current.DefaultCurrency,
            SupportedCurrencies = current.SupportedCurrencies,
            MaxFileUploadSizeMB = current.MaxFileUploadSizeMB,
            SignalREnabled = current.SignalREnabled,
            BotEnabled = current.BotEnabled,
            Documents = new DocumentSettings
            {
                CvTemplate = string.IsNullOrWhiteSpace(body.CvTemplate)
                    ? CvTemplates.Normalise(current.Documents.CvTemplate)
                    : CvTemplates.Normalise(body.CvTemplate),
            },
            Intake = new IntakeDefaults
            {
                Gender = body.Gender is "0" or "1" ? body.Gender : "",
                Occupation = (body.Occupation ?? "").Trim(),
                Religion = (body.Religion ?? "").Trim(),
                Nationality = (body.Nationality ?? "").Trim(),
                PassportType = (body.PassportType ?? "").Trim(),
                MaritalStatus = (body.MaritalStatus ?? "").Trim(),
                CountryOfTravel = (body.CountryOfTravel ?? "").Trim(),
                ContractPeriod = (body.ContractPeriod ?? "").Trim(),
            },
        };
    }

    /// <summary>
    /// Explicit camelCase names so the settings page and the new-candidate form read the same
    /// object even if HTTP JSON naming is not applied to inferred anonymous-type properties.
    /// </summary>
    public static object ToDto(TenantSettings settings) => new
    {
        gender = settings.Intake.Gender,
        occupation = settings.Intake.Occupation,
        religion = settings.Intake.Religion,
        nationality = settings.Intake.Nationality,
        passportType = settings.Intake.PassportType,
        maritalStatus = settings.Intake.MaritalStatus,
        countryOfTravel = settings.Intake.CountryOfTravel,
        contractPeriod = settings.Intake.ContractPeriod,
        cvTemplate = CvTemplates.Normalise(settings.Documents.CvTemplate),
        cvTemplates = CvTemplates.All
            .Select(x => new { value = x.Value, name = x.Name, description = x.Description }),
    };

    private static async Task<Domain.Entities.Identity.TenantInfo?> LoadAsync(
        IPlatformDbContext db, ICurrentUserService user) =>
        user.TenantId is Guid id
            ? await db.Tenants.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted)
            : null;
}
