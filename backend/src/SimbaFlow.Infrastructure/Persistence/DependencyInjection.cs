using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Infrastructure.Audit;
using SimbaFlow.Infrastructure.BackgroundJobs;
using SimbaFlow.Infrastructure.DomainEvents;
using SimbaFlow.Infrastructure.Identity;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured.");

        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey) || System.Text.Encoding.UTF8.GetBytes(jwtKey).Length < 32)
            throw new InvalidOperationException(
                "Jwt:Key must be configured with at least 32 bytes.");

        // Database with tenant schema isolation
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                connectionString,
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            options.AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // DbContextFactory for TenantSchemaResolver (needs independent contexts)
        services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(sp =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            return new SimpleDbContextFactory(optionsBuilder.Options, sp);
        });

        services.AddScoped<ITenantSchemaResolver, TenantSchemaResolver>();

        // Tenant context (resolved from current user's JWT claims)
        services.AddScoped<ITenantContext>(sp =>
        {
            var currentUser = sp.GetRequiredService<ICurrentUserService>();
            return new TenantContext(currentUser.TenantId, null, currentUser.TenantId.HasValue ? null : null, currentUser.IsSuperAdmin);
        });

        // Authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)),
                ClockSkew = TimeSpan.FromMinutes(1),
            };
        });

        // ASP.NET Core Identity
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = int.Parse(configuration["Identity:Password:RequiredLength"] ?? "8");
            options.Password.RequireUppercase = bool.Parse(configuration["Identity:Password:RequireUppercase"] ?? "true");
            options.Password.RequireLowercase = bool.Parse(configuration["Identity:Password:RequireLowercase"] ?? "true");
            options.Password.RequireDigit = bool.Parse(configuration["Identity:Password:RequireDigit"] ?? "true");
            options.Password.RequireNonAlphanumeric = bool.Parse(configuration["Identity:Password:RequireNonAlphanumeric"] ?? "true");
            options.Lockout.MaxFailedAccessAttempts = int.Parse(configuration["Identity:Lockout:MaxFailedAttempts"] ?? "5");
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
                int.Parse(configuration["Identity:Lockout:LockoutDurationMinutes"] ?? "15"));
            options.Lockout.AllowedForNewUsers = true;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders()
        .AddPasswordValidator<PasswordHistoryValidator>();

        // Identity services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Staff context
        services.AddScoped<StaffContext>();
        services.AddScoped<IStaffContext>(provider => provider.GetRequiredService<StaffContext>());
        services.AddMemoryCache();

        // File Storage
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // Read Audit (high-throughput via Channel)
        var readAuditChannel = System.Threading.Channels.Channel.CreateUnbounded<ReadAuditEntry>(
            new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });
        services.AddSingleton(readAuditChannel);
        services.AddSingleton<IReadAuditService, ReadAuditService>();
        services.AddHostedService<ReadAuditBatchWriter>();

        // Background services
        services.AddHostedService<TokenCleanupService>();
        services.AddHostedService<SessionCleanupService>();

        return services;
    }
}
