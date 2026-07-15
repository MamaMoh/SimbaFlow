using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds the default SuperAdmin user and system roles for SimbaFlow labour export platform.
/// Idempotent: only creates entities that don't already exist.
/// </summary>
public static class AdminSeeder
{
    public static async Task SeedDefaultAdminAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        // Seed system roles for labour export agency
        await SeedRoleAsync(roleManager, "SuperAdmin", "Full system administration access", isSystem: true);
        await SeedRoleAsync(roleManager, "AgencyOwner", "Agency owner / tenant super-admin", isSystem: true);
        await SeedRoleAsync(roleManager, "OfficeManager", "Branch office manager", isSystem: true);
        await SeedRoleAsync(roleManager, "EmbassyOfficer", "Embassy processing officer", isSystem: true);
        await SeedRoleAsync(roleManager, "CaseExecutive", "Visa documentation case executive", isSystem: true);
        await SeedRoleAsync(roleManager, "FinanceOfficer", "Finance and commission management", isSystem: true);
        await SeedRoleAsync(roleManager, "FieldAgent", "Field agent (mobile/bot access)", isSystem: true);
        await SeedRoleAsync(roleManager, "DataEntryClerk", "Candidate registration and data entry", isSystem: true);
        await SeedRoleAsync(roleManager, "Auditor", "Read-only audit access", isSystem: true);
        await SeedRoleAsync(roleManager, "NotificationManager", "Notification and bot configuration", isSystem: true);

        // Seed default SuperAdmin user
        const string adminUsername = "admin";
        const string adminEmail = "admin@simbaflow.local";
        const string adminPassword = "Admin@123!";

        var existingAdmin = await userManager.FindByNameAsync(adminUsername);
        if (existingAdmin is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminUsername,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Administrator",
                IsSuperAdmin = true,
                IsActive = true,
                IsFirstLogin = false,
                MustChangePassword = false,
                EmailConfirmed = true,
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "SuperAdmin");
                logger.LogInformation("Default SuperAdmin user created: {Username}", adminUsername);
            }
            else
            {
                logger.LogWarning("Failed to create SuperAdmin: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedRoleAsync(
        RoleManager<ApplicationRole> roleManager, string name, string description, bool isSystem)
    {
        if (!await roleManager.RoleExistsAsync(name))
        {
            await roleManager.CreateAsync(new ApplicationRole
            {
                Name = name,
                Description = description,
                IsSystemRole = isSystem,
            });
        }
    }
}
