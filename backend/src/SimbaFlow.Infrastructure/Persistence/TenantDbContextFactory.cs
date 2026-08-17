using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for generating TenantDbContext EF migrations.
/// </summary>
public class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=simbaflow;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(connectionString, b =>
                b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName)
                    .MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TenantDbContext(options, new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUserService
    {
        public string? UserId => null;
        public string? UserName => "design-time";
        public string? Email => null;
        public Guid? ActiveLocationId => null;
        public Guid? DepartmentId => null;
        public Guid? TenantId => null;
        public IReadOnlyList<string> Permissions => [];
        public IReadOnlyList<string> Roles => [];
        public bool IsSuperAdmin => true;
        public bool HasPermission(string permission) => true;
        public string? IpAddress => null;
        public string? UserAgent => null;
    }
}
