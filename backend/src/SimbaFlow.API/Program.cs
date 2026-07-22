using Carter;
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

    if (app.Environment.IsDevelopment())
    {
        var dbContext = services.GetRequiredService<SimbaFlow.Infrastructure.Persistence.ApplicationDbContext>();
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
    }

    await PermissionSeeder.SeedPermissionsAsync(services);
    await PermissionSeeder.SeedDefaultTenantAsync(services);
    await AdminSeeder.SeedDefaultAdminAsync(services);
    await DepartmentSeeder.SeedDepartmentsAsync(services);
    await LocationSeeder.SeedLocationsAsync(services);
    await RolePermissionSeeder.SeedRolePermissionsAsync(services);
    await OfficeSeeder.SeedOfficesAsync(services);
    await WorkflowSeeder.SeedDefaultWorkflowAsync(services);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<GlobalExceptionHandler>();
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
