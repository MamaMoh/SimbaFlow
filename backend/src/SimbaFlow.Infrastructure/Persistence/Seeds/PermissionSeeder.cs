using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds permission codes for all Phase 1 modules.
/// Idempotent: only creates permissions that don't already exist.
/// </summary>
public static class PermissionSeeder
{
    private static readonly (string Module, string Code, string Name)[] Permissions =
    [
        // Auth
        ("auth", "auth.login", "Login to system"),

        // Users
        ("users", "users.read", "View users"),
        ("users", "users.write", "Create/update/delete users"),

        // Roles
        ("role", "role.read", "View roles and permissions"),
        ("role", "role.write", "Create/update/delete roles"),

        // Candidates
        ("candidate", "candidate.read", "View candidates"),
        ("candidate", "candidate.create", "Register new candidates"),
        ("candidate", "candidate.update", "Update candidate information"),
        ("candidate", "candidate.delete", "Archive/delete candidates"),

        // Workflow
        ("workflow", "workflow.view", "View workflow stage views"),
        ("workflow", "workflow.execute", "Execute workflow transitions"),
        ("workflow", "workflow.configure", "Configure workflow stages, statuses, and rules"),

        // Embassy
        ("embassy", "embassy.read", "View embassy processing stage"),
        ("embassy", "embassy.update", "Update medical, Tasheer, and visa status"),
        ("embassy", "embassy.case_view", "View Case Executive board"),
        ("embassy", "embassy.case_submit", "Submit visa documentation (Case Executive)"),
        ("embassy", "embassy.visa_outcome", "Record visa Issued/Rejected and resubmit"),

        // LMIS
        ("lmis", "lmis.read", "View LMIS stage"),
        ("lmis", "lmis.update", "Update insurance, milestone status"),
        ("lmis", "lmis.document", "Upload LMIS documents"),

        // Travel & Logistics
        ("travel", "travel.read", "View ticket and departure views"),
        ("travel", "travel.update", "Book tickets, manage departures"),

        // Arrival
        ("arrival", "arrival.read", "View arrival and exception views"),
        ("arrival", "arrival.update", "Confirm arrivals, flag exceptions"),
        ("arrival", "arrival.exception", "Manage exception containment workspace"),

        // Commission & Finance
        ("commission", "commission.read", "View commission records"),
        ("commission", "commission.create", "Initialize commission records"),
        ("commission", "commission.update", "Record payments, manage disputes"),

        // Accounting
        ("accounting", "accounting.read", "View accounts, journal entries, statements"),
        ("accounting", "accounting.post", "Post journal entries"),
        ("accounting", "accounting.reconcile", "Perform bank reconciliation"),

        // Staff
        ("staff", "staff.read", "View staff profiles"),
        ("staff", "staff.create", "Create staff profiles"),
        ("staff", "staff.update", "Update staff profiles"),
        ("staff", "staff.terminate", "Terminate staff members"),

        // Partners
        ("partner", "partner.read", "View partner agencies and employers"),
        ("partner", "partner.create", "Create partner agencies"),
        ("partner", "partner.update", "Update partner agencies"),

        // Notifications
        ("notification", "notification.configure", "Configure notification rules and templates"),
        ("notification", "notification.send", "Send manual notifications"),

        // Bot
        ("bot", "bot.configure", "Configure bot connections and settings"),
        ("bot", "bot.use", "Use bot commands (field agents)"),

        // Reports
        ("report", "report.view", "View reports and dashboards"),
        ("report", "report.export", "Export reports to Excel/PDF"),
        ("report", "report.schedule", "Configure scheduled reports"),

        // Tenant Administration
        ("tenant", "tenant.provision", "Provision new tenants/agencies"),
        ("tenant", "tenant.manage", "Manage tenant settings and status"),

        // Audit
        ("audit", "audit.read", "View audit trail"),

        // Settings
        ("settings", "settings.read", "View settings"),
        ("settings", "settings.write", "Manage settings"),

        // System
        ("system", "system.admin", "Full system administration"),
    ];
    public static async Task SeedPermissionsAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        var validCodes = Permissions.Select(p => p.Code).ToHashSet();

        var existingPermissions = await context.Permissions
            .IgnoreQueryFilters()
            .ToListAsync();

        var existingCodes = existingPermissions.Select(p => p.Code).ToHashSet();

        // Add new permissions
        var toAdd = Permissions
            .Where(p => !existingCodes.Contains(p.Code))
            .Select(p => new Permission
            {
                Code = p.Code,
                Name = p.Name,
                Module = p.Module,
                IsActive = true,
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            context.Permissions.AddRange(toAdd);
            logger.LogInformation("Seeded {Count} new permissions", toAdd.Count);
        }

        // Reactivate valid permissions that were previously deactivated
        // (e.g. re-introduced after being removed from an earlier code version)
        var toReactivate = existingPermissions
            .Where(p => validCodes.Contains(p.Code) && (!p.IsActive || p.IsDeleted))
            .ToList();

        foreach (var p in toReactivate)
        {
            p.IsActive = true;
            p.IsDeleted = false;
        }

        if (toReactivate.Count > 0)
            logger.LogInformation("Reactivated {Count} permissions", toReactivate.Count);

        // Deactivate permissions that are no longer in the valid list
        var toDeactivate = existingPermissions
            .Where(p => !validCodes.Contains(p.Code) && p.IsActive)
            .ToList();

        foreach (var p in toDeactivate)
        {
            p.IsActive = false;
            p.IsDeleted = true;
        }

        if (toDeactivate.Count > 0)
            logger.LogInformation("Deactivated {Count} legacy permissions", toDeactivate.Count);

        if (toAdd.Count > 0 || toReactivate.Count > 0 || toDeactivate.Count > 0)
            await context.SaveChangesAsync();
    }

    public static async Task SeedDefaultTenantAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        if (!await context.Tenants.AnyAsync())
        {
            context.Tenants.Add(new TenantInfo
            {
                Name = "Default Agency",
                Slug = "default-agency",
                SchemaName = "tenant_default_agency",
                ContactEmail = "admin@simbaflow.local",
                SubscriptionStatus = Domain.Enums.TenantStatus.Active,
                AgencyLevel = 3,
                LicenseStatus = Domain.Enums.AgencyLicenseStatus.Active,
                LicensedCountries = ["Saudi Arabia", "United Arab Emirates", "Kuwait"],
                Country = "Ethiopia",
            });
            await context.SaveChangesAsync();
        }
    }
}
