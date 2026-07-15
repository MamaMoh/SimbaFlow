using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Tenants.Commands;

public record ProvisionTenantCommand(
    string AgencyName,
    string Slug,
    string ContactEmail,
    string? ContactPhone,
    string AdminFirstName,
    string AdminLastName,
    string AdminEmail,
    string TemporaryPassword) : IRequest<Result<Guid>>;

public class ProvisionTenantHandler : IRequestHandler<ProvisionTenantCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<ProvisionTenantHandler> _logger;

    public ProvisionTenantHandler(
        IApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<ProvisionTenantHandler> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(ProvisionTenantCommand request, CancellationToken cancellationToken)
    {
        // Validate slug uniqueness
        var slugExists = await _context.Tenants
            .AnyAsync(t => t.Slug == request.Slug, cancellationToken);

        if (slugExists)
            return Result<Guid>.Failure("A tenant with this slug already exists.", 409);

        // Validate admin email uniqueness
        var emailExists = await _userManager.FindByEmailAsync(request.AdminEmail);
        if (emailExists is not null)
            return Result<Guid>.Failure("A user with this email already exists.", 409);

        var schemaName = $"tenant_{request.Slug.Replace("-", "_")}";

        // 1. Create tenant record
        var tenant = new TenantInfo
        {
            Name = request.AgencyName,
            Slug = request.Slug,
            SchemaName = schemaName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            SubscriptionStatus = TenantStatus.Active,
            ProvisionedAt = DateTime.UtcNow,
            Settings = new TenantSettings()
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync(cancellationToken);

        // 2. Create PostgreSQL schema for this tenant and apply migrations
        try
        {
            var dbContext = _context as Microsoft.EntityFrameworkCore.DbContext;
            if (dbContext != null)
            {
                var conn = dbContext.Database.GetDbConnection();
                var wasOpen = conn.State == System.Data.ConnectionState.Open;
                if (!wasOpen) await conn.OpenAsync(cancellationToken);

                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);

                    cmd.CommandText = $@"
                        CREATE TABLE IF NOT EXISTS ""{schemaName}"".candidates (
                            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                            first_name VARCHAR(100) NOT NULL, last_name VARCHAR(100) NOT NULL,
                            middle_name VARCHAR(100), passport_number VARCHAR(20) NOT NULL,
                            labour_id VARCHAR(50), date_of_birth DATE NOT NULL,
                            gender SMALLINT NOT NULL DEFAULT 0, nationality VARCHAR(100),
                            phone_number VARCHAR(20), email VARCHAR(200),
                            address VARCHAR(500), city VARCHAR(100), country VARCHAR(100),
                            country_of_travel VARCHAR(100), office_name VARCHAR(200),
                            contract_date DATE,
                            office_id UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                            photo_path VARCHAR(500), status SMALLINT NOT NULL DEFAULT 0,
                            current_stage_id UUID, current_stage_name VARCHAR(100),
                            current_status_values TEXT DEFAULT '{{}}',
                            visible_in_stages TEXT DEFAULT '[]',
                            registered_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                            registered_by VARCHAR(200),
                            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                            created_by VARCHAR(200), updated_at TIMESTAMPTZ,
                            updated_by VARCHAR(200),
                            is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                            row_version INTEGER NOT NULL DEFAULT 0
                        );
                        CREATE TABLE IF NOT EXISTS ""{schemaName}"".tenant_roles (
                            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                            name VARCHAR(100) NOT NULL, code VARCHAR(100) NOT NULL,
                            description TEXT, is_system_role BOOLEAN NOT NULL DEFAULT FALSE,
                            is_active BOOLEAN NOT NULL DEFAULT TRUE,
                            sort_order INTEGER NOT NULL DEFAULT 0,
                            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                            created_by VARCHAR(200), updated_at TIMESTAMPTZ,
                            updated_by VARCHAR(200),
                            is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                            row_version INTEGER NOT NULL DEFAULT 0
                        );
                        CREATE TABLE IF NOT EXISTS ""{schemaName}"".tenant_role_permissions (
                            tenant_role_id UUID NOT NULL, permission_code VARCHAR(100) NOT NULL,
                            granted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), granted_by VARCHAR(200),
                            PRIMARY KEY (tenant_role_id, permission_code)
                        );
                        CREATE TABLE IF NOT EXISTS ""{schemaName}"".tenant_user_roles (
                            user_id UUID NOT NULL, tenant_role_id UUID NOT NULL,
                            assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), assigned_by VARCHAR(200),
                            PRIMARY KEY (user_id, tenant_role_id)
                        );
                    ";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    _logger.LogInformation("Created schema {Schema} with tables", schemaName);
                }
                finally
                {
                    if (!wasOpen) await conn.CloseAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create schema/tables for {Schema}", schemaName);
        }

        // 3. Create Agency Owner user account
        var adminUser = new ApplicationUser
        {
            UserName = request.AdminEmail, // Use email as username
            Email = request.AdminEmail,
            FirstName = request.AdminFirstName,
            LastName = request.AdminLastName,
            TenantId = tenant.Id,
            IsActive = true,
            IsFirstLogin = false,
            MustChangePassword = false,
            EmailConfirmed = true,
        };

        var createResult = await _userManager.CreateAsync(adminUser, request.TemporaryPassword);
        if (!createResult.Succeeded)
        {
            // Rollback tenant creation
            _context.Tenants.Remove(tenant);
            await _context.SaveChangesAsync(cancellationToken);

            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result<Guid>.Failure($"Failed to create admin user: {errors}", 400);
        }

        // 3. Assign AgencyOwner role
        if (await _roleManager.RoleExistsAsync("AgencyOwner"))
        {
            await _userManager.AddToRoleAsync(adminUser, "AgencyOwner");
        }

        _logger.LogInformation(
            "Provisioned tenant {TenantName} (schema: {Schema}) with admin user {AdminEmail}",
            request.AgencyName, schemaName, request.AdminEmail);

        return Result<Guid>.Success(tenant.Id, 201);
    }
}
