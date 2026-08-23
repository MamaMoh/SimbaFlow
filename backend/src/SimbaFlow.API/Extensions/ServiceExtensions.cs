using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using SimbaFlow.Application.Common.Behaviors;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.RealTime;

namespace SimbaFlow.API.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Carter (Minimal API modules)
        services.AddCarter();

        // MediatR + Pipeline Behaviors (order matters)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(ApplicationDbContext).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceLogBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(Program).Assembly);

        // Infrastructure (EF Core + Identity + JWT + services)
        services.AddInfrastructure(configuration);

        // SignalR
        services.AddSignalR();
        services.AddScoped<ISignalRBroadcaster, SignalRBroadcaster>();

        // HTTP Context
        services.AddHttpContextAccessor();

        // Authorization
        var authBuilder = services.AddAuthorizationBuilder()
            .AddPolicy("SuperAdmin", policy => policy.RequireAssertion(ctx =>
                ctx.User.HasClaim("role", "SuperAdmin") ||
                ctx.User.HasClaim(System.Security.Claims.ClaimTypes.Role, "SuperAdmin") ||
                ctx.User.IsInRole("SuperAdmin")));

        // Rate Limiting
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("login", opt =>
            {
                opt.PermitLimit = 5;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 0;
            });

            // Password reset accepts an arbitrary email from an anonymous caller. Keep it tight:
            // enough for a person who mistypes their address, far too slow to enumerate accounts.
            options.AddFixedWindowLimiter("auth", opt =>
            {
                opt.PermitLimit = 5;
                opt.Window = TimeSpan.FromMinutes(15);
                opt.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("refresh", opt =>
            {
                opt.PermitLimit = 30;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("general", opt =>
            {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 2;
            });

            options.AddFixedWindowLimiter("upload", opt =>
            {
                opt.PermitLimit = 10;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 0;
            });
        });

        // CORS
        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(
                    configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"])
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        // Health Checks
        services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("DefaultConnection")!, name: "postgresql");

        // OpenAPI
        services.AddOpenApi();

        return services;
    }
}
