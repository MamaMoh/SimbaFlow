using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.Infrastructure.Services.Bot;

public interface ITenantBotDbContextFactory
{
    /// <summary>
    /// Open a tenant-scoped context for a bot command.
    ///
    /// <paramref name="tenantId"/> may be null for a platform admin, who has no tenant of their
    /// own — the interceptor falls back to the default agency schema for them, which is what the
    /// web app already does. Refusing them here instead would lock the super admin out of the bot
    /// while the same account works fine in the browser.
    /// </summary>
    Task<TenantDbContext> CreateAsync(Guid? tenantId, bool isSuperAdmin, CancellationToken ct = default);
}

public sealed class TenantBotDbContextFactory : ITenantBotDbContextFactory
{
    private readonly string _connectionString;
    private readonly ITenantSchemaResolver _schemaResolver;
    private readonly ILoggerFactory _loggerFactory;

    public TenantBotDbContextFactory(
        IConfiguration configuration,
        ITenantSchemaResolver schemaResolver,
        ILoggerFactory loggerFactory)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
        _schemaResolver = schemaResolver;
        _loggerFactory = loggerFactory;
    }

    public async Task<TenantDbContext> CreateAsync(Guid? tenantId, bool isSuperAdmin, CancellationToken ct = default)
    {
        var currentUser = new BotCurrentUserService(tenantId, isSuperAdmin);
        var interceptor = new TenantConnectionInterceptor(
            currentUser,
            _schemaResolver,
            _loggerFactory.CreateLogger<TenantConnectionInterceptor>());

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(_connectionString, b =>
                b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName)
                    .MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        return new TenantDbContext(options, currentUser);
    }

    private sealed class BotCurrentUserService(Guid? tenantId, bool isSuperAdmin) : ICurrentUserService
    {
        public string? UserId => null;
        public string? UserName => "bot";
        public string? Email => null;
        public Guid? ActiveLocationId => null;
        public Guid? DepartmentId => null;
        public Guid? TenantId => tenantId;
        public IReadOnlyList<string> Permissions => [];
        public IReadOnlyList<string> Roles => [];
        public bool IsSuperAdmin => isSuperAdmin;
        public bool HasPermission(string permission) => false;
        public string? IpAddress => null;
        public string? UserAgent => "telegram-bot";
    }
}
