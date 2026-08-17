using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Infrastructure.Persistence;

/// <summary>
/// Resolves TenantId → PostgreSQL schema name with in-memory caching.
/// Uses a short-lived DbContext to query the public.tenants table.
/// </summary>
public class TenantSchemaResolver : ITenantSchemaResolver
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<PlatformDbContext> _dbContextFactory;
    private readonly ILogger<TenantSchemaResolver> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public TenantSchemaResolver(
        IMemoryCache cache,
        IDbContextFactory<PlatformDbContext> dbContextFactory,
        ILogger<TenantSchemaResolver> logger)
    {
        _cache = cache;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<string?> ResolveSchemaAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"tenant_schema:{tenantId}";

        if (_cache.TryGetValue(cacheKey, out string? cachedSchema))
            return cachedSchema;

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var tenant = await context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId && !t.IsDeleted)
            .Select(t => new { t.SchemaName, t.SubscriptionStatus })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            _logger.LogWarning("Tenant {TenantId} not found", tenantId);
            return null;
        }

        if (tenant.SubscriptionStatus != TenantStatus.Active)
        {
            _logger.LogWarning("Tenant {TenantId} is not active (status: {Status})", tenantId, tenant.SubscriptionStatus);
            return null;
        }

        _cache.Set(cacheKey, tenant.SchemaName, CacheDuration);
        return tenant.SchemaName;
    }

    public async Task<string?> ResolveDefaultSchemaAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "tenant_schema:default";
        if (_cache.TryGetValue(cacheKey, out string? cachedSchema))
            return cachedSchema;

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var schema = await context.Tenants
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.SubscriptionStatus == TenantStatus.Active)
            .OrderBy(t => t.SchemaName == "tenant_default_agency" ? 0 : 1)
            .ThenBy(t => t.ProvisionedAt)
            .Select(t => t.SchemaName)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(schema))
            _cache.Set(cacheKey, schema, CacheDuration);

        return schema;
    }

    public void InvalidateCache(Guid tenantId)
    {
        _cache.Remove($"tenant_schema:{tenantId}");
    }
}
