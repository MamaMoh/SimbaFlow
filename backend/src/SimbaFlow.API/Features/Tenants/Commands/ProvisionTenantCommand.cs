using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Persistence.Seeds;

namespace SimbaFlow.API.Features.Tenants.Commands;

public record ProvisionTenantCommand(
    string AgencyName,
    string Slug,
    string ContactEmail,
    string? ContactPhone,
    string AdminFirstName,
    string AdminLastName,
    string AdminEmail,
    string TemporaryPassword,
    int AgencyLevel = 5,
    string? LicenseNumber = null,
    string? LicenseIssuedAt = null,
    string? LicenseExpiresAt = null,
    List<string>? LicensedCountries = null,
    string? Address = null,
    string? City = null,
    string? Country = null) : IRequest<Result<Guid>>;

public class ProvisionTenantHandler : IRequestHandler<ProvisionTenantCommand, Result<Guid>>
{
    private readonly IPlatformDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ITenantSchemaMigrator _tenantMigrator;
    private readonly IWorkflowDefinitionUpgrader _workflowUpgrader;
    private readonly IFinanceSeedService _financeSeed;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProvisionTenantHandler> _logger;

    public ProvisionTenantHandler(
        IPlatformDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ITenantSchemaMigrator tenantMigrator,
        IWorkflowDefinitionUpgrader workflowUpgrader,
        IFinanceSeedService financeSeed,
        IConfiguration configuration,
        ILogger<ProvisionTenantHandler> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _tenantMigrator = tenantMigrator;
        _workflowUpgrader = workflowUpgrader;
        _financeSeed = financeSeed;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(ProvisionTenantCommand request, CancellationToken cancellationToken)
    {
        if (!AgencyLevelRules.IsValidLevel(request.AgencyLevel))
            return Result<Guid>.Failure("Agency level must be between 1 and 5 (MoLS ደረጃ).", 400);

        var countries = (request.LicensedCountries ?? [])
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!AgencyLevelRules.CountriesWithinLimit(request.AgencyLevel, countries.Count))
        {
            var (_, maxCountries) = AgencyLevelRules.GetCaps(request.AgencyLevel);
            return Result<Guid>.Failure(
                $"Level {request.AgencyLevel} may license at most {maxCountries} destination countries (Arts. 18–22).",
                400);
        }

        DateOnly? issued = null;
        DateOnly? expires = null;
        if (!string.IsNullOrWhiteSpace(request.LicenseIssuedAt) &&
            !DateOnly.TryParse(request.LicenseIssuedAt, out var issuedParsed))
            return Result<Guid>.Failure("License issued date is invalid.", 400);
        else if (!string.IsNullOrWhiteSpace(request.LicenseIssuedAt))
            issued = DateOnly.Parse(request.LicenseIssuedAt!);

        if (!string.IsNullOrWhiteSpace(request.LicenseExpiresAt) &&
            !DateOnly.TryParse(request.LicenseExpiresAt, out var expiresParsed))
            return Result<Guid>.Failure("License expiry date is invalid.", 400);
        else if (!string.IsNullOrWhiteSpace(request.LicenseExpiresAt))
            expires = DateOnly.Parse(request.LicenseExpiresAt!);

        if (issued is not null && expires is not null && expires < issued)
            return Result<Guid>.Failure("License expiry must be on or after the issue date.", 400);

        var slugExists = await _context.Tenants
            .AnyAsync(t => t.Slug == request.Slug, cancellationToken);

        if (slugExists)
            return Result<Guid>.Failure("A tenant with this slug already exists.", 409);

        var emailExists = await _userManager.FindByEmailAsync(request.AdminEmail);
        if (emailExists is not null)
            return Result<Guid>.Failure("A user with this email already exists.", 409);

        var schemaName = $"tenant_{request.Slug.Replace("-", "_")}";
        var (maxPartnersPerCountry, maxCountriesCap) = AgencyLevelRules.GetCaps(request.AgencyLevel);

        var tenant = new TenantInfo
        {
            Name = request.AgencyName,
            Slug = request.Slug,
            SchemaName = schemaName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            City = request.City,
            Country = request.Country ?? "Ethiopia",
            SubscriptionStatus = TenantStatus.Active,
            ProvisionedAt = DateTime.UtcNow,
            Settings = new TenantSettings(),
            AgencyLevel = request.AgencyLevel,
            LicenseNumber = string.IsNullOrWhiteSpace(request.LicenseNumber) ? null : request.LicenseNumber.Trim(),
            LicenseIssuedAt = issued,
            LicenseExpiresAt = expires,
            LicenseStatus = string.IsNullOrWhiteSpace(request.LicenseNumber)
                ? AgencyLicenseStatus.Pending
                : AgencyLicenseStatus.Active,
            LicensedCountries = countries,
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _tenantMigrator.EnsureSchemaAndMigrateAsync(schemaName, cancellationToken);

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                await WorkflowSeeder.SeedDefaultWorkflowIntoSchemaAsync(
                    connectionString, schemaName, tenant.Id, cancellationToken);
                await _workflowUpgrader.EnsureUnit3ArtifactsIntoSchemaAsync(
                    connectionString, schemaName, tenant.Id, cancellationToken);
                await _workflowUpgrader.EnsureUnit4ArtifactsIntoSchemaAsync(
                    connectionString, schemaName, tenant.Id, cancellationToken);
                await _financeSeed.EnsureUnit5ArtifactsIntoSchemaAsync(
                    connectionString, schemaName, tenant.Id, cancellationToken);
                _logger.LogInformation(
                    "Seeded default workflow for schema {Schema} (level {Level}: ≤{PerCountry}/country, countries cap {CountryCap})",
                    schemaName, tenant.AgencyLevel, maxPartnersPerCountry,
                    maxCountriesCap?.ToString() ?? "unlimited");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to migrate/seed schema {Schema}", schemaName);
        }

        var adminUser = new ApplicationUser
        {
            UserName = request.AdminEmail,
            Email = request.AdminEmail,
            FirstName = request.AdminFirstName,
            LastName = request.AdminLastName,
            TenantId = tenant.Id,
            IsActive = true,
            IsFirstLogin = true,
            MustChangePassword = true,
            EmailConfirmed = true,
        };

        var createResult = await _userManager.CreateAsync(adminUser, request.TemporaryPassword);
        if (!createResult.Succeeded)
        {
            _context.Tenants.Remove(tenant);
            await _context.SaveChangesAsync(cancellationToken);

            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result<Guid>.Failure($"Failed to create admin user: {errors}", 400);
        }

        if (await _roleManager.RoleExistsAsync("AgencyOwner"))
        {
            await _userManager.AddToRoleAsync(adminUser, "AgencyOwner");
        }

        _logger.LogInformation(
            "Provisioned tenant {TenantName} (schema: {Schema}, level {Level}) with admin {AdminEmail}",
            request.AgencyName, schemaName, tenant.AgencyLevel, request.AdminEmail);

        return Result<Guid>.Success(tenant.Id, 201);
    }
}
