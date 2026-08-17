using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Tenants.Commands;
using SimbaFlow.API.Features.Tenants.Queries;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Tenants;

public class TenantModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants")
            .WithTags("Tenants")
            .RequireAuthorization("SuperAdmin");

        group.MapGet("/agency-levels", () =>
        {
            var levels = Enumerable.Range(AgencyLevelRules.MinLevel, AgencyLevelRules.MaxLevel)
                .Select(level =>
                {
                    var (perCountry, maxCountries) = AgencyLevelRules.GetCaps(level);
                    return new
                    {
                        level,
                        maxPartnersPerCountry = perCountry,
                        maxCountries,
                        description = AgencyLevelRules.Describe(level)
                    };
                });
            return Results.Ok(new { isSuccess = true, data = levels });
        });

        group.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetTenantsQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/{id:guid}", async (Guid id, IPlatformDbContext context) =>
        {
            var tenant = await context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Tenant not found" }, statusCode: 404);

            var owner = await context.ApplicationUsers
                .AsNoTracking()
                .Where(u => u.TenantId == id && !u.IsDeleted)
                .OrderBy(u => u.CreatedAt)
                .Select(u => new { u.FirstName, u.LastName, u.Email })
                .FirstOrDefaultAsync();

            var (perCountry, maxCountries) = AgencyLevelRules.GetCaps(tenant.AgencyLevel);
            var activeLinks = await context.PartnerLinks.CountAsync(l =>
                l.TenantId == id && !l.IsDeleted && l.Status == Domain.Enums.PartnerLinkStatus.Active);

            return Results.Ok(new
            {
                isSuccess = true,
                data = new
                {
                    tenant.Id,
                    tenant.Name,
                    tenant.Slug,
                    tenant.SchemaName,
                    tenant.ContactEmail,
                    tenant.ContactPhone,
                    tenant.Address,
                    tenant.City,
                    tenant.Country,
                    Status = (int)tenant.SubscriptionStatus,
                    tenant.MaxUsers,
                    tenant.ProvisionedAt,
                    tenant.AgencyLevel,
                    tenant.LicenseNumber,
                    LicenseIssuedAt = tenant.LicenseIssuedAt?.ToString("yyyy-MM-dd"),
                    LicenseExpiresAt = tenant.LicenseExpiresAt?.ToString("yyyy-MM-dd"),
                    LicenseStatus = (int)tenant.LicenseStatus,
                    tenant.LicensedCountries,
                    MaxPartnersPerCountry = perCountry,
                    MaxCountries = maxCountries,
                    ActivePartnerLinks = activeLinks,
                    LevelDescription = AgencyLevelRules.Describe(tenant.AgencyLevel),
                    OwnerFirstName = owner?.FirstName,
                    OwnerLastName = owner?.LastName,
                    OwnerEmail = owner?.Email,
                }
            });
        });

        group.MapPost("/", async (ProvisionTenantCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/tenants/{result.Data}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateTenantRequest request, IPlatformDbContext context) =>
        {
            var tenant = await context.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Tenant not found" }, statusCode: 404);

            if (request.AgencyLevel is int level)
            {
                if (!AgencyLevelRules.IsValidLevel(level))
                    return Results.Json(new { isSuccess = false, error = "Agency level must be 1–5." }, statusCode: 400);
                tenant.AgencyLevel = level;
            }

            var countries = request.LicensedCountries;
            if (countries is not null)
            {
                countries = countries
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (!AgencyLevelRules.CountriesWithinLimit(tenant.AgencyLevel, countries.Count))
                {
                    var (_, maxCountries) = AgencyLevelRules.GetCaps(tenant.AgencyLevel);
                    return Results.Json(new
                    {
                        isSuccess = false,
                        error = $"Level {tenant.AgencyLevel} may license at most {maxCountries} destination countries."
                    }, statusCode: 400);
                }
                tenant.LicensedCountries = countries;
            }

            if (request.LicenseIssuedAt is not null)
            {
                if (string.IsNullOrWhiteSpace(request.LicenseIssuedAt))
                    tenant.LicenseIssuedAt = null;
                else if (!DateOnly.TryParse(request.LicenseIssuedAt, out var issued))
                    return Results.Json(new { isSuccess = false, error = "License issued date is invalid." }, statusCode: 400);
                else
                    tenant.LicenseIssuedAt = issued;
            }

            if (request.LicenseExpiresAt is not null)
            {
                if (string.IsNullOrWhiteSpace(request.LicenseExpiresAt))
                    tenant.LicenseExpiresAt = null;
                else if (!DateOnly.TryParse(request.LicenseExpiresAt, out var expires))
                    return Results.Json(new { isSuccess = false, error = "License expiry date is invalid." }, statusCode: 400);
                else
                    tenant.LicenseExpiresAt = expires;
            }

            if (tenant.LicenseIssuedAt is not null
                && tenant.LicenseExpiresAt is not null
                && tenant.LicenseExpiresAt < tenant.LicenseIssuedAt)
            {
                return Results.Json(new { isSuccess = false, error = "License expiry must be on or after the issue date." },
                    statusCode: 400);
            }

            if (request.LicenseStatus is int statusInt)
            {
                if (!Enum.IsDefined(typeof(Domain.Enums.AgencyLicenseStatus), statusInt))
                    return Results.Json(new { isSuccess = false, error = "Invalid license status." }, statusCode: 400);
                tenant.LicenseStatus = (Domain.Enums.AgencyLicenseStatus)statusInt;
            }
            else if (!string.IsNullOrWhiteSpace(request.LicenseStatusName)
                     && Enum.TryParse<Domain.Enums.AgencyLicenseStatus>(request.LicenseStatusName, true, out var statusByName))
            {
                tenant.LicenseStatus = statusByName;
            }

            tenant.Name = request.Name;
            tenant.ContactEmail = request.ContactEmail;
            tenant.ContactPhone = request.ContactPhone;
            tenant.MaxUsers = request.MaxUsers ?? tenant.MaxUsers;
            if (request.LicenseNumber is not null) tenant.LicenseNumber = request.LicenseNumber;
            if (request.Address is not null) tenant.Address = request.Address;
            if (request.City is not null) tenant.City = request.City;
            if (request.Country is not null) tenant.Country = request.Country;

            await context.SaveChangesAsync();
            return Results.Ok(new { isSuccess = true });
        });

        group.MapPut("/{id:guid}/status", async (Guid id, UpdateTenantStatusRequest request, ISender sender) =>
        {
            var command = new UpdateTenantStatusCommand(id, request.Status);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IPlatformDbContext context) =>
        {
            var tenant = await context.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Tenant not found" }, statusCode: 404);

            tenant.IsDeleted = true;
            tenant.SubscriptionStatus = Domain.Enums.TenantStatus.Deactivated;
            await context.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}

public record UpdateTenantStatusRequest(Domain.Enums.TenantStatus Status);
public record UpdateTenantRequest(
    string Name,
    string ContactEmail,
    string? ContactPhone,
    int? MaxUsers,
    int? AgencyLevel = null,
    string? LicenseNumber = null,
    List<string>? LicensedCountries = null,
    string? Address = null,
    string? City = null,
    string? Country = null,
    string? LicenseIssuedAt = null,
    string? LicenseExpiresAt = null,
    int? LicenseStatus = null,
    string? LicenseStatusName = null);
