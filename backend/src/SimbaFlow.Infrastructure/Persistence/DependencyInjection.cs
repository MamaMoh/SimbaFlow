using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Infrastructure.Options;
using SimbaFlow.Infrastructure.Audit;
using SimbaFlow.Infrastructure.BackgroundJobs;
using SimbaFlow.Infrastructure.DomainEvents;
using SimbaFlow.Infrastructure.Identity;
using SimbaFlow.Infrastructure.Services;
using SimbaFlow.Infrastructure.Services.Bot;
using SimbaFlow.Infrastructure.Workflow;
using SimbaFlow.Infrastructure.Persistence.Seeds;

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

        // ═══════════════════════════════════════════════════════════════
        // 1. PLATFORM DbContext (public schema — Identity, Tenants, Audit)
        //    NO interceptor. Always queries "public" schema.
        // ═══════════════════════════════════════════════════════════════
        services.AddDbContext<PlatformDbContext>(options =>
            options.UseNpgsql(connectionString,
                b => b.MigrationsAssembly(typeof(PlatformDbContext).Assembly.FullName)));

        services.AddScoped<IPlatformDbContext>(sp => sp.GetRequiredService<PlatformDbContext>());

        // ═══════════════════════════════════════════════════════════════
        // 2. TENANT DbContext (dynamic schema — Candidates, Workflow, Roles)
        //    Has TenantConnectionInterceptor that sets search_path.
        // ═══════════════════════════════════════════════════════════════
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddDbContext<TenantDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, b =>
                b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName)
                    .MigrationsHistoryTable("__ef_migrations_history"));
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());
        });

        services.AddScoped<ITenantDbContext>(sp => sp.GetRequiredService<TenantDbContext>());
        services.AddScoped<ITenantSchemaMigrator, TenantSchemaMigrator>();

        // Keep legacy IApplicationDbContext pointing to PlatformDbContext for now
        // (gradually migrate handlers to use IPlatformDbContext or ITenantDbContext)
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // ═══════════════════════════════════════════════════════════════
        // 3. Tenant Schema Resolution (for the interceptor)
        // ═══════════════════════════════════════════════════════════════
        services.AddSingleton<IDbContextFactory<PlatformDbContext>>(sp =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            return new PlatformDbContextFactory(optionsBuilder.Options, sp);
        });

        services.AddScoped<ITenantSchemaResolver, TenantSchemaResolver>();

        // Tenant context (from JWT claims)
        services.AddScoped<ITenantContext>(sp =>
        {
            var currentUser = sp.GetRequiredService<ICurrentUserService>();
            return new TenantContext(currentUser.TenantId, null, null, currentUser.IsSuperAdmin);
        });

        // ═══════════════════════════════════════════════════════════════
        // 4. Authentication (JWT)
        // ═══════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════
        // 5. ASP.NET Core Identity (uses PlatformDbContext)
        // ═══════════════════════════════════════════════════════════════
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
        .AddEntityFrameworkStores<PlatformDbContext>()
        .AddDefaultTokenProviders()
        .AddPasswordValidator<PasswordHistoryValidator>();

        // ═══════════════════════════════════════════════════════════════
        // 6. Application Services
        // ═══════════════════════════════════════════════════════════════
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IWorkflowEngineService, WorkflowEngineService>();
        services.AddScoped<ICvGenerationService, CvGenerationService>();
        services.AddScoped<IReportExportService, Services.Reporting.ReportExportService>();
        services.AddScoped<IWorkflowDefinitionUpgrader, WorkflowDefinitionUpgrader>();
        services.AddScoped<ICandidateNotifier, TelegramCandidateNotifier>();
        services.AddScoped<IFinanceSeedService, FinanceSeedService>();
        services.AddScoped<IExchangeRateService, ExchangeRateService>();
        services.AddScoped<IJournalPostingService, JournalPostingService>();
        services.Configure<TelegramOptions>(configuration.GetSection("Telegram"));
        services.Configure<MfaOptions>(configuration.GetSection("Mfa"));
        // Telegram puts the bot token in the URL path (api.telegram.org/bot<TOKEN>/method), and the
        // default HttpClient logging handler writes the full request URI at Information level — which
        // wrote the token in clear text into every log file and log shipper. Strip those loggers; the
        // gateway logs the method name itself, which is all we need for diagnostics.
        services.AddHttpClient<ITelegramGateway, TelegramGateway>().RemoveAllLoggers();
        services.AddSingleton<ITelegramPollerState, TelegramPollerState>();
        services.AddScoped<ITenantBotDbContextFactory, TenantBotDbContextFactory>();
        services.AddScoped<IBotLinkService, BotLinkService>();
        services.AddScoped<ITelegramCommandDispatcher, TelegramCommandDispatcher>();
        services.AddScoped<INotificationPushService, NotificationPushService>();

        // Staff context
        services.AddScoped<StaffContext>();
        services.AddScoped<IStaffContext>(provider => provider.GetRequiredService<StaffContext>());
        services.AddMemoryCache();

        // Read Audit (high-throughput via Channel)
        var readAuditChannel = System.Threading.Channels.Channel.CreateUnbounded<ReadAuditEntry>(
            new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });
        services.AddSingleton(readAuditChannel);
        services.AddSingleton<IReadAuditService, ReadAuditService>();
        services.AddHostedService<ReadAuditBatchWriter>();

        // Background services
        services.AddHostedService<TokenCleanupService>();
        services.AddHostedService<SessionCleanupService>();
        services.AddHostedService<TelegramPollingService>();

        return services;
    }
}

/// <summary>
/// Factory for creating PlatformDbContext instances (used by TenantSchemaResolver).
/// </summary>
internal class PlatformDbContextFactory : IDbContextFactory<PlatformDbContext>
{
    private readonly DbContextOptions<PlatformDbContext> _options;
    private readonly IServiceProvider _serviceProvider;

    public PlatformDbContextFactory(DbContextOptions<PlatformDbContext> options, IServiceProvider serviceProvider)
    {
        _options = options;
        _serviceProvider = serviceProvider;
    }

    public PlatformDbContext CreateDbContext()
    {
        var currentUser = new NoOpCurrentUserService();
        return new PlatformDbContext(_options, currentUser);
    }

    private class NoOpCurrentUserService : ICurrentUserService
    {
        public string? UserId => null;
        public string? UserName => null;
        public string? Email => null;
        public Guid? ActiveLocationId => null;
        public Guid? DepartmentId => null;
        public Guid? TenantId => null;
        public IReadOnlyList<string> Permissions => [];
        public IReadOnlyList<string> Roles => [];
        public bool IsSuperAdmin => false;
        public bool HasPermission(string permission) => false;
        public string? IpAddress => null;
        public string? UserAgent => null;
    }
}
