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
            "partner.read", "partner.create", "partner.update",
            // The agency owner administers their own tenant's bot: configure the connection,
            // monitor deliveries, and link their own Telegram account.
            "notification.configure", "notification.send",
            "bot.configure", "bot.use",
            "report.view", "report.export", "report.schedule",
            "audit.read",
            "role.read", "role.write",
            "users.read", "users.write",
            // The agency configures its own intake defaults and preferences.
            "settings.read", "settings.write",
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
            // Partner agreements are negotiated and maintained by the office manager, so
            // registering a partner belongs with them rather than only the agency owner.
            "partner.read", "partner.create", "partner.update",
            // Branch managers work in the field alongside agents, so they can link their own bot.
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
            // The agent held embassy.update and arrival.update without the matching reads, so
            // both boards were hidden from them and neither permission could be exercised
            // anywhere — the bot answers /medical and /arrived with "use the web app".
            "embassy.read", "embassy.update",
            "arrival.read", "arrival.update",
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
            // Configuring the bot without being able to use it meant this role could set the
            // connection up and then not link its own Telegram account to test it.
            "bot.configure", "bot.use",
            "settings.read",
        ],
        // Platform administration without agency access: user accounts, roles, tenants and
        // system settings. Deliberately holds no candidate.read — this role exists so someone
        // can run the platform without being able to read any agency's people.
        ["PlatformAdmin"] = [
            "users.read", "users.write",
            "role.read", "role.write",
            "staff.read",
            // Deliberately not tenant.manage/provision: creating and suspending agencies is
            // reserved for SuperAdmin, and the tenants endpoint is gated on that role anyway —
            // granting them here would only produce a link that fails.
            "settings.read", "settings.write",
            "audit.read",
            "notification.configure",
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
    /// Seeds one user per role so each role can actually be signed into and exercised.
    ///
    /// Agency roles are attached to <paramref name="tenantId"/> — without a tenant they sign in
    /// successfully and then meet a permission error on every page, which is how the platform
    /// admin account ended up unusable. PlatformAdmin is deliberately left with no tenant: it
    /// administers the platform and is not supposed to reach any agency's candidates.
    ///
    /// Idempotent — an existing username is left alone, including its password.
    /// </summary>
    public static async Task SeedRoleUsersAsync(
        IServiceProvider serviceProvider, Guid tenantId, string password)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        var testUsers = new[]
        {
            new { Username = "owner.amir", Email = "amir@simbaflow.local", FirstName = "Amir", LastName = "Hassan", Role = "AgencyOwner", StaffType = StaffType.AgencyOwner, Platform = false },
            new { Username = "mgr.hana", Email = "hana@simbaflow.local", FirstName = "Hana", LastName = "Bekele", Role = "OfficeManager", StaffType = StaffType.OfficeManager, Platform = false },
            new { Username = "embassy.dawit", Email = "dawit@simbaflow.local", FirstName = "Dawit", LastName = "Fikru", Role = "EmbassyOfficer", StaffType = StaffType.EmbassyOfficer, Platform = false },
            new { Username = "case.sara", Email = "sara@simbaflow.local", FirstName = "Sara", LastName = "Ahmed", Role = "CaseExecutive", StaffType = StaffType.CaseExecutive, Platform = false },
            new { Username = "fin.yonas", Email = "yonas@simbaflow.local", FirstName = "Yonas", LastName = "Tadesse", Role = "FinanceOfficer", StaffType = StaffType.FinanceOfficer, Platform = false },
            new { Username = "field.kebede", Email = "kebede@simbaflow.local", FirstName = "Kebede", LastName = "Girma", Role = "FieldAgent", StaffType = StaffType.FieldAgent, Platform = false },
            new { Username = "clerk.tigist", Email = "tigist@simbaflow.local", FirstName = "Tigist", LastName = "Wondwosen", Role = "DataEntryClerk", StaffType = StaffType.DataEntryClerk, Platform = false },
            new { Username = "audit.abebe", Email = "abebe@simbaflow.local", FirstName = "Abebe", LastName = "Assefa", Role = "Auditor", StaffType = StaffType.Auditor, Platform = false },
            new { Username = "notify.selam", Email = "selam@simbaflow.local", FirstName = "Selam", LastName = "Getachew", Role = "NotificationManager", StaffType = StaffType.DataEntryClerk, Platform = false },
            new { Username = "platform.rediet", Email = "rediet@simbaflow.local", FirstName = "Rediet", LastName = "Alemu", Role = "PlatformAdmin", StaffType = StaffType.AgencyOwner, Platform = true },
        };

        var created = 0;
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
                TenantId = tu.Platform ? null : tenantId,
                IsActive = true,
                IsFirstLogin = false,
                MustChangePassword = false,
                EmailConfirmed = true,
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogWarning("Could not create {Username}: {Errors}",
                    tu.Username, string.Join(", ", result.Errors.Select(e => e.Description)));
                continue;
            }

            await userManager.AddToRoleAsync(user, tu.Role);
            context.StaffProfiles.Add(new StaffProfile
            {
                UserId = user.Id,
                FirstName = tu.FirstName,
                LastName = tu.LastName,
                StaffType = tu.StaffType,
                EmploymentStatus = EmploymentStatus.Active,
                HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6)),
            });
            created++;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Role users: {Created} created, {Skipped} already present",
            created, testUsers.Length - created);
    }
}
