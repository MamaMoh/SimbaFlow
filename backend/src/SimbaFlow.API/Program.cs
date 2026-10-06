using Carter;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using SimbaFlow.API.Extensions;
using SimbaFlow.API.Middleware;
using SimbaFlow.Infrastructure.Persistence.Seeds;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "SimbaFlow")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/simbaflow-.json",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 100_000_000,
        outputTemplate: "{Timestamp:o} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddApiServices(builder.Configuration);

// Keep the data-protection key ring on the storage volume rather than inside the container.
//
// It defaulted to /app/.aspnet/DataProtection-Keys, which is container-local, so every deploy
// started with fresh keys. Password-reset tokens are protected with these: someone who clicked
// "forgot password", read their email and followed the link after a release found the link
// rejected, with nothing to say why. The volume at FileStorage:BasePath already outlives the
// container, and the application name pins the ring so a rename cannot silently orphan it.
var keyRingPath = Path.Combine(
    builder.Configuration["FileStorage:BasePath"] ?? "/data", "data-protection-keys");
try
{
    Directory.CreateDirectory(keyRingPath);
    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
        .SetApplicationName("SimbaFlow");
}
catch (Exception ex)
{
    // A read-only or missing volume is not a reason to refuse to start; it is a reason to say
    // that reset links will not survive the next deploy.
    Log.Warning(ex,
        "Could not persist data-protection keys to {Path}; password reset links will stop working "
        + "when this container is replaced", keyRingPath);
}

var app = builder.Build();

// Apply migrations and seed data on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // Migrations ran only in Development while the seeders below run unconditionally, so a fresh
    // Production deployment seeded against an unmigrated database and crashed on startup.
    // Self-hosted deployments opt in with Database__MigrateOnStartup=true.
    var migrateOnStartup = app.Environment.IsDevelopment()
        || app.Configuration.GetValue("Database:MigrateOnStartup", false);

    if (migrateOnStartup)
    {
        var dbContext = services.GetRequiredService<SimbaFlow.Infrastructure.Persistence.PlatformDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        try
        {
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Database migration failed. Fix migrations and run: dotnet ef database update");
            throw;
        }

        try
        {
            var tenantMigrator = services.GetRequiredService<SimbaFlow.Infrastructure.Persistence.ITenantSchemaMigrator>();
            await tenantMigrator.MigrateAllActiveTenantsAsync();

            var configuration = services.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                var tenants = await dbContext.Tenants
                    .AsNoTracking()
                    .Where(t => !t.IsDeleted && t.SubscriptionStatus == SimbaFlow.Domain.Enums.TenantStatus.Active)
                    .Select(t => new { t.Id, t.SchemaName })
                    .ToListAsync();

                foreach (var tenant in tenants)
                {
                    try
                    {
                        await WorkflowSeeder.SeedDefaultWorkflowIntoSchemaAsync(
                            connectionString,
                            tenant.SchemaName,
                            tenant.Id);

                        var upgrader = services.GetRequiredService<IWorkflowDefinitionUpgrader>();
                        await upgrader.EnsureUnit3ArtifactsIntoSchemaAsync(
                            connectionString,
                            tenant.SchemaName,
                            tenant.Id);
                        await upgrader.EnsureUnit4ArtifactsIntoSchemaAsync(
                            connectionString,
                            tenant.SchemaName,
                            tenant.Id);

                        var financeSeed = services.GetRequiredService<SimbaFlow.Application.Common.Interfaces.IFinanceSeedService>();
                        await financeSeed.EnsureUnit5ArtifactsIntoSchemaAsync(
                            connectionString,
                            tenant.SchemaName,
                            tenant.Id);
                    }
                    catch (Exception seedEx)
                    {
                        logger.LogWarning(seedEx,
                            "Workflow seed/upgrade skipped/failed for schema {Schema}", tenant.SchemaName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Per-tenant failures are logged inside the migrator; only unexpected aggregate failures land here.
            logger.LogError(ex, "Tenant schema migration failed");
        }
    }

    // What the system needs to run at all: the permission catalogue, the roles that carry those
    // permissions, the departments an agency is organised into, and one administrator to sign in.
    await PermissionSeeder.SeedPermissionsAsync(services);
    await AdminSeeder.SeedDefaultAdminAsync(services);
    await DepartmentSeeder.SeedDepartmentsAsync(services);
    await RolePermissionSeeder.SeedRolePermissionsAsync(services);

    // Sample business records: a demonstration agency called "Default Agency", and a starter
    // catalogue of ten foreign partner agencies. These are what the system needs to look
    // populated, not what it needs to work — and they used to come back by themselves, so
    // emptying the database and restarting handed you a tenant and ten partners you did not
    // create. Off unless asked for, so a fresh install comes up empty and the first agency on it
    // is a real one. Both are idempotent and only act on empty tables, so an existing deployment
    // is unaffected whichever way this is set.
    if (app.Configuration.GetValue("Seeding:SampleData", false))
    {
        await PermissionSeeder.SeedDefaultTenantAsync(services);
        await PartnerAgencySeeder.SeedPartnerAgenciesAsync(services);
    }

    // One user per role, for trying each role out. Opt-in rather than automatic: these are real
    // sign-ins, so they appear only when someone names a tenant to attach them to, and the
    // setting can be removed again once the accounts exist.
    var roleUsersTenant = builder.Configuration["Seeding:RoleUsersTenantId"];
    if (Guid.TryParse(roleUsersTenant, out var seedTenantId))
    {
        var seedPassword = builder.Configuration["Seeding:RoleUsersPassword"];
        var seedLogger = services.GetRequiredService<ILogger<Program>>();
        if (string.IsNullOrWhiteSpace(seedPassword))
            seedLogger.LogWarning("Seeding:RoleUsersTenantId is set but Seeding:RoleUsersPassword is not — skipping role users");
        else
            await RolePermissionSeeder.SeedRoleUsersAsync(services, seedTenantId, seedPassword);
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Must run before anything that reads the caller's address — the rate limiter partitions on it.
// Requests reach this API only from nginx and the Next.js server, both on the private Docker
// network, so the forwarded address can be trusted; nothing off-host can reach port 8100 to forge
// one. Without this every request appears to come from the proxy, and a per-IP limit would put the
// entire platform in one bucket.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    // The chain is browser → nginx → Next.js → here, so more than one hop is expected.
    ForwardLimit = 3,
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
foreach (var network in new[]
         {
             new System.Net.IPNetwork(IPAddress.Parse("127.0.0.0"), 8),
             new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8),
             new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12),
             new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16),
         })
{
    forwardedHeaders.KnownIPNetworks.Add(network);
}
app.UseForwardedHeaders(forwardedHeaders);

app.UseMiddleware<GlobalExceptionHandler>();
app.UseMiddleware<SecurityHeadersMiddleware>();

// HSTS outside development, where the certificate is usually self-signed or absent.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SimbaFlow.Infrastructure.Identity.StaffContextMiddleware>();
app.MapCarter();
app.MapHub<SimbaFlow.Infrastructure.RealTime.SimbaFlowHub>("/hubs/simbaflow");
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
