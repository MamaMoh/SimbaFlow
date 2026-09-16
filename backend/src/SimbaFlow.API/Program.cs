using Carter;
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

    await PermissionSeeder.SeedPermissionsAsync(services);
    await PermissionSeeder.SeedDefaultTenantAsync(services);
    await AdminSeeder.SeedDefaultAdminAsync(services);
    await DepartmentSeeder.SeedDepartmentsAsync(services);
    await PartnerAgencySeeder.SeedPartnerAgenciesAsync(services);
    await RolePermissionSeeder.SeedRolePermissionsAsync(services);

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
