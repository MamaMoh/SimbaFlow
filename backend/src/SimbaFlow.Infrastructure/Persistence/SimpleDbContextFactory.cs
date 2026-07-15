using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Persistence;

/// <summary>
/// Simple DbContext factory for TenantSchemaResolver that creates
/// independent DbContext instances without scoped service dependencies.
/// </summary>
public class SimpleDbContextFactory : IDbContextFactory<ApplicationDbContext>
{
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IServiceProvider _serviceProvider;

    public SimpleDbContextFactory(DbContextOptions<ApplicationDbContext> options, IServiceProvider serviceProvider)
    {
        _options = options;
        _serviceProvider = serviceProvider;
    }

    public ApplicationDbContext CreateDbContext()
    {
        // Use a no-op current user for factory-created contexts (used for schema resolution only)
        var currentUser = new NoOpCurrentUserService();
        return new ApplicationDbContext(_options, currentUser);
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
