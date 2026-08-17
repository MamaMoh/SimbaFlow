using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.Infrastructure.Services;

public sealed class FinanceSeedService : IFinanceSeedService
{
    public const string CashBankCode = "1100";
    public const string ArCommissionsCode = "1200";
    public const string ClearingCode = "2100";
    public const string CommissionRevenueCode = "4100";

    private readonly ILogger<FinanceSeedService> _logger;

    public FinanceSeedService(ILogger<FinanceSeedService> logger)
    {
        _logger = logger;
    }

    public async Task EnsureUnit5ArtifactsIntoSchemaAsync(
        string connectionString,
        string schemaName,
        Guid tenantId,
        CancellationToken ct = default)
    {
        await using var ctx = CreateContext(connectionString, schemaName, tenantId);
        await EnsureDefaultChartOfAccountsAsync(ctx, ct);
        await EnsureUnit5BackfillAsync(ctx, ct);
    }

    public async Task EnsureDefaultChartOfAccountsAsync(ITenantDbContext context, CancellationToken ct = default)
    {
        var defaults = new (string Code, string Name, AccountType Type)[]
        {
            (CashBankCode, "Cash / Bank", AccountType.Asset),
            (ArCommissionsCode, "Accounts Receivable — Commissions", AccountType.Asset),
            (ClearingCode, "Clearing / Suspense", AccountType.Liability),
            (CommissionRevenueCode, "Commission Revenue", AccountType.Revenue),
        };

        foreach (var (code, name, type) in defaults)
        {
            var existing = await context.Accounts
                .FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted, ct);

            if (existing is null)
            {
                context.Accounts.Add(new Account
                {
                    Code = code,
                    Name = name,
                    Type = type,
                    Currency = "ETB",
                    IsSystem = true,
                    IsActive = true,
                });
            }
            else
            {
                existing.Name = name;
                existing.Type = type;
                existing.IsSystem = true;
                existing.IsActive = true;
                existing.Currency = string.IsNullOrWhiteSpace(existing.Currency) ? "ETB" : existing.Currency;
            }
        }

        await context.SaveChangesAsync(ct);
        _logger.LogInformation("Ensured default chart of accounts ({Count} accounts)", defaults.Length);
    }

    public async Task EnsureUnit5BackfillAsync(ITenantDbContext context, CancellationToken ct = default)
    {
        var hasCounter = await context.FinanceCounters.AnyAsync(c => !c.IsDeleted, ct);
        if (!hasCounter)
        {
            context.FinanceCounters.Add(new FinanceCounter { NextJournalNumber = 1 });
            await context.SaveChangesAsync(ct);
        }

        // Totals columns default to 0 via migration; nothing else required for backfill in v1.
        _logger.LogDebug("Unit 5 finance backfill complete");
    }

    private static TenantDbContext CreateContext(string connectionString, string schemaName, Guid tenantId)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = schemaName
        };

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TenantDbContext(options, new SeedCurrentUser(tenantId));
    }

    private sealed class SeedCurrentUser(Guid tenantId) : ICurrentUserService
    {
        public string? UserId => null;
        public string? UserName => "system";
        public string? Email => null;
        public Guid? ActiveLocationId => null;
        public Guid? DepartmentId => null;
        public Guid? TenantId => tenantId;
        public IReadOnlyList<string> Permissions => [];
        public IReadOnlyList<string> Roles => [];
        public bool IsSuperAdmin => true;
        public bool HasPermission(string permission) => true;
        public string? IpAddress => null;
        public string? UserAgent => null;
    }
}
