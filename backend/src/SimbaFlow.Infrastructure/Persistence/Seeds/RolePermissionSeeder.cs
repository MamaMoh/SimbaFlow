using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Staff;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds role-permission mappings and test users for development.
/// </summary>
public static class RolePermissionSeeder
{
    private static readonly Dictionary<string, string[]> RolePermissionMap = new()
    {
        ["AgencyOwner"] = [
            "candidate.read", "candidate.create", "candidate.update", "candidate.delete",
            "workflow.view", "workflow.execute", "workflow.configure",
            "embassy.read", "embassy.update", "embassy.case_view", "embassy.case_submit", "embassy.visa_outcome",
            "lmis.read", "lmis.update", "lmis.document",
            "travel.read", "travel.update",
            "arrival.read", "arrival.update", "arrival.exception",
            "commission.read", "commission.create", "commission.update",
            "accounting.read", "accounting.post", "accounting.reconcile",
            "staff.read", "staff.create", "staff.update", "staff.terminate",
            "office.read", "office.write",
            "partner.read", "partner.create", "partner.update",
            // The agency owner administers their own tenant's bot: configure the connection,
            // monitor deliveries, and link their own Telegram account.
            "notification.configure", "notification.send",
            "bot.configure", "bot.use",
            "report.view", "report.export", "report.schedule",
            "audit.read",
            "role.read", "role.write",
            "users.read", "users.write",
        ],
        ["OfficeManager"] = [
            "candidate.read", "candidate.create", "candidate.update",
            "workflow.view", "workflow.execute",
            "embassy.read", "embassy.update", "embassy.case_view", "embassy.case_submit", "embassy.visa_outcome",
            "lmis.read", "lmis.update", "lmis.document",
            "travel.read", "travel.update",
            "arrival.read", "arrival.update", "arrival.exception",
            "commission.read",
            "accounting.read",
            "staff.read",
            "office.read",
            "partner.read",
            // Office managers work in the field alongside agents, so they can link their own bot.
            "bot.use",
            "report.view", "report.export",
        ],
        ["EmbassyOfficer"] = [
            "candidate.read",
            "workflow.view", "workflow.execute",
            "embassy.read", "embassy.update", "embassy.visa_outcome",
            "lmis.read", "lmis.update", "lmis.document",
        ],
        ["CaseExecutive"] = [
            "candidate.read",
            "workflow.view",
            "embassy.case_view", "embassy.case_submit",
        ],
        ["FinanceOfficer"] = [
            "candidate.read",
            "commission.read", "commission.create", "commission.update",
            "accounting.read", "accounting.post", "accounting.reconcile",
            "report.view", "report.export",
        ],
        ["FieldAgent"] = [
            "candidate.read", "candidate.update",
            "workflow.view", "workflow.execute",
            "embassy.update",
            "arrival.update",
            "bot.use",
        ],
        ["DataEntryClerk"] = [
            "candidate.read", "candidate.create", "candidate.update",
            "workflow.view",
        ],
        ["Auditor"] = [
            "candidate.read",
            "workflow.view",
            "embassy.read",
            "lmis.read",
            "travel.read",
            "arrival.read",
            "commission.read",
            "accounting.read",
            "audit.read",
            "report.view", "report.export",
        ],
        ["NotificationManager"] = [
            "notification.configure", "notification.send",
            "bot.configure",
        ],
    };

    public static async Task SeedRolePermissionsAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        var allPermissions = await context.Permissions
            .Where(p => p.IsActive && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Code, p => p.Id);

        foreach (var (roleName, permissionCodes) in RolePermissionMap)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) continue;

            var existingMappings = await context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToHashSetAsync();

            foreach (var code in permissionCodes)
            {
                if (!allPermissions.TryGetValue(code, out var permId)) continue;
                if (existingMappings.Contains(permId)) continue;

                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permId,
                    GrantedBy = "System",
                });
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded role-permission mappings for labour export roles");
    }

    /// <summary>
    /// Seeds test users for development environment.
    /// </summary>
    public static async Task SeedTestUsersAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        var testUsers = new[]
        {
            new { Username = "owner.amir", Email = "amir@simbaflow.local", FirstName = "Amir", LastName = "Hassan", Role = "AgencyOwner", StaffType = StaffType.AgencyOwner },
            new { Username = "mgr.hana", Email = "hana@simbaflow.local", FirstName = "Hana", LastName = "Bekele", Role = "OfficeManager", StaffType = StaffType.OfficeManager },
            new { Username = "embassy.dawit", Email = "dawit@simbaflow.local", FirstName = "Dawit", LastName = "Fikru", Role = "EmbassyOfficer", StaffType = StaffType.EmbassyOfficer },
            new { Username = "case.sara", Email = "sara@simbaflow.local", FirstName = "Sara", LastName = "Ahmed", Role = "CaseExecutive", StaffType = StaffType.CaseExecutive },
            new { Username = "fin.yonas", Email = "yonas@simbaflow.local", FirstName = "Yonas", LastName = "Tadesse", Role = "FinanceOfficer", StaffType = StaffType.FinanceOfficer },
            new { Username = "field.kebede", Email = "kebede@simbaflow.local", FirstName = "Kebede", LastName = "Girma", Role = "FieldAgent", StaffType = StaffType.FieldAgent },
            new { Username = "clerk.tigist", Email = "tigist@simbaflow.local", FirstName = "Tigist", LastName = "Wondwosen", Role = "DataEntryClerk", StaffType = StaffType.DataEntryClerk },
            new { Username = "audit.abebe", Email = "abebe@simbaflow.local", FirstName = "Abebe", LastName = "Assefa", Role = "Auditor", StaffType = StaffType.Auditor },
        };

        foreach (var tu in testUsers)
        {
            if (await userManager.FindByNameAsync(tu.Username) is not null)
                continue;

            var user = new ApplicationUser
            {
                UserName = tu.Username,
                Email = tu.Email,
                FirstName = tu.FirstName,
                LastName = tu.LastName,
                IsActive = true,
                IsFirstLogin = false,
                MustChangePassword = false,
                EmailConfirmed = true,
            };

            var result = await userManager.CreateAsync(user, "Test@123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, tu.Role);

                // Create linked StaffProfile
                context.StaffProfiles.Add(new StaffProfile
                {
                    UserId = user.Id,
                    FirstName = tu.FirstName,
                    LastName = tu.LastName,
                    StaffType = tu.StaffType,
                    EmploymentStatus = EmploymentStatus.Active,
                    HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6)),
                });
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} test users for development", testUsers.Length);
    }
}
