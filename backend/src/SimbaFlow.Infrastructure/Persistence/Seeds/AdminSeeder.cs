using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds the system roles, and — only on a database that has no platform administrator yet —
/// one bootstrap account to sign in with.
///
/// The roles are safe to seed on every boot. The account is not: this runs unconditionally at
/// startup, including in production, so a fixed username and password written here would be a
/// standing back door published with the source. Instead the password comes from configuration,
/// and when none is supplied a random one is generated and written to the log once. Either way the
/// account must change it at first sign-in.
/// </summary>
public static class AdminSeeder
{
    /// <summary>The roles the application itself relies on. Seeded on every boot; creating a role that already exists is a no-op.</summary>
    private static readonly (string Name, string Description)[] SystemRoles =
    [
        ("SuperAdmin", "Full system administration access"),
        ("AgencyOwner", "Agency owner / tenant super-admin"),
        ("OfficeManager", "Branch office manager"),
        ("EmbassyOfficer", "Embassy processing officer"),
        ("CaseExecutive", "Visa documentation case executive"),
        ("FinanceOfficer", "Finance and commission management"),
        ("FieldAgent", "Field agent (mobile/bot access)"),
        ("DataEntryClerk", "Candidate registration and data entry"),
        ("Auditor", "Read-only audit access"),
        ("NotificationManager", "Notification and bot configuration"),
        // Platform-level, but deliberately not SuperAdmin: user accounts, roles and system
        // configuration only. No candidate or pipeline access, so support staff can administer
        // the platform without being able to read any agency's people.
        ("PlatformAdmin", "Platform user accounts and system configuration"),
    ];

    public static async Task SeedDefaultAdminAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        foreach (var (name, description) in SystemRoles)
            await SeedRoleAsync(roleManager, name, description);

        // Once someone holds the platform, we have no business creating another way into it.
        // This is also what stops a later deploy from re-seeding an account an operator deleted
        // on purpose.
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var platformAdminExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.IsSuperAdmin && !u.IsDeleted);

        if (platformAdminExists)
            return;

        var username = configuration["Seed:AdminUsername"] ?? "admin";
        var email = configuration["Seed:AdminEmail"] ?? "admin@simbaflow.local";

        // A supplied password is used as given; otherwise one is invented that nobody — including
        // anyone reading this repository — knows until they read the log line below.
        var configured = configuration["Seed:AdminPassword"];
        var generated = string.IsNullOrWhiteSpace(configured);
        var password = generated ? GeneratePassword() : configured!;

        var admin = new ApplicationUser
        {
            UserName = username,
            Email = email,
            FirstName = "System",
            LastName = "Administrator",
            IsSuperAdmin = true,
            IsActive = true,
            // Whoever ends up with this account proves themselves by choosing a new password. That
            // holds for a configured password too: it has been through a deploy pipeline to get here.
            IsFirstLogin = true,
            MustChangePassword = true,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Could not create the bootstrap administrator: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, "SuperAdmin");

        if (generated)
        {
            // Said once, at startup, and never again — this is the only place it appears. It is
            // logged at Warning so it survives a production log level that drops Information.
            logger.LogWarning(
                "Created the bootstrap administrator {Username} with a generated password: {Password}"
                + " — sign in and change it now. This will not be shown again.",
                username, password);
        }
        else
        {
            logger.LogInformation(
                "Created the bootstrap administrator {Username} from Seed:AdminPassword. It must change"
                + " its password at first sign-in.",
                username);
        }
    }

    /// <summary>
    /// Long enough that it is not worth attacking, and drawn from the alphabet the password policy
    /// asks for so that <see cref="UserManager{T}.CreateAsync(T, string)"/> does not reject it.
    /// </summary>
    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*-_=+";
        const string all = upper + lower + digits + symbols;

        // One of each class first, so the result satisfies the policy no matter how the shuffle falls.
        var chars = new List<char>
        {
            Pick(upper), Pick(lower), Pick(digits), Pick(symbols),
        };
        while (chars.Count < 24) chars.Add(Pick(all));

        // Fisher-Yates, so the guaranteed characters do not always sit in the first four positions.
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars.ToArray());

        static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
    }

    private static async Task SeedRoleAsync(
        RoleManager<ApplicationRole> roleManager, string name, string description)
    {
        if (!await roleManager.RoleExistsAsync(name))
        {
            await roleManager.CreateAsync(new ApplicationRole
            {
                Name = name,
                Description = description,
                IsSystemRole = true,
            });
        }
    }
}
