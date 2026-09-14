using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Tenancy;

namespace SimbaFlow.API.Features.Tenants;

public record IntakeDefaultsBody(
    string Gender,
    string Occupation,
    string Religion,
    string Nationality,
    string PassportType,
    string MaritalStatus,
    string CountryOfTravel,
    string ContractPeriod);

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
            return Results.Ok(new { isSuccess = true, data = tenant?.Settings.Intake ?? new IntakeDefaults() });
        });

        group.MapPut("/", async (
            IntakeDefaultsBody body, IPlatformDbContext db, ICurrentUserService user) =>
        {
            if (!user.HasPermission("settings.write") && !user.IsSuperAdmin)
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var tenant = await LoadAsync(db, user);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "No agency on this account." }, statusCode: 400);

            // Reassigning the whole object matters: Settings is stored as JSON through a value
            // converter, which only notices a change when the reference changes.
            tenant.Settings = new TenantSettings
            {
                DefaultLanguage = tenant.Settings.DefaultLanguage,
                SupportedLanguages = tenant.Settings.SupportedLanguages,
                DefaultCurrency = tenant.Settings.DefaultCurrency,
                SupportedCurrencies = tenant.Settings.SupportedCurrencies,
                MaxFileUploadSizeMB = tenant.Settings.MaxFileUploadSizeMB,
                SignalREnabled = tenant.Settings.SignalREnabled,
                BotEnabled = tenant.Settings.BotEnabled,
                Intake = new IntakeDefaults
                {
                    Gender = body.Gender is "0" or "1" ? body.Gender : "1",
                    Occupation = (body.Occupation ?? "").Trim(),
                    Religion = (body.Religion ?? "").Trim(),
                    Nationality = (body.Nationality ?? "").Trim(),
                    PassportType = (body.PassportType ?? "").Trim(),
                    MaritalStatus = (body.MaritalStatus ?? "").Trim(),
                    CountryOfTravel = (body.CountryOfTravel ?? "").Trim(),
                    ContractPeriod = (body.ContractPeriod ?? "").Trim(),
                },
            };

            await db.SaveChangesAsync(default);
            return Results.Ok(new { isSuccess = true, data = tenant.Settings.Intake });
        });
    }

    private static async Task<Domain.Entities.Identity.TenantInfo?> LoadAsync(
        IPlatformDbContext db, ICurrentUserService user) =>
        user.TenantId is Guid id
            ? await db.Tenants.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted)
            : null;
}
