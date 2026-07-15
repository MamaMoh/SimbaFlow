using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Services;

public class TenantSchemaResolverTests
{
    [Fact]
    public async Task ResolveSchemaAsync_ActiveTenant_ReturnsCachedSchemaName()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var schemaName = "tenant_acme_agency";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        // We need a factory for the resolver — use a simple mock approach
        var cache = new MemoryCache(new MemoryCacheOptions());

        // Pre-populate cache to test cache hit
        cache.Set($"tenant_schema:{tenantId}", schemaName, TimeSpan.FromMinutes(5));

        var resolver = new TenantSchemaResolver(
            cache,
            null!, // DbContextFactory not needed for cache hit test
            NullLogger<TenantSchemaResolver>.Instance);

        // Act
        var result = await resolver.ResolveSchemaAsync(tenantId);

        // Assert
        Assert.Equal(schemaName, result);
    }

    [Fact]
    public void InvalidateCache_RemovesCachedEntry()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set($"tenant_schema:{tenantId}", "tenant_test", TimeSpan.FromMinutes(5));

        var resolver = new TenantSchemaResolver(
            cache,
            null!,
            NullLogger<TenantSchemaResolver>.Instance);

        // Act
        resolver.InvalidateCache(tenantId);

        // Assert
        Assert.False(cache.TryGetValue($"tenant_schema:{tenantId}", out _));
    }
}
