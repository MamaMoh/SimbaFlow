using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.Infrastructure.Services;

public sealed class FinanceSeedService : IFinanceSeedService
{
    public const string CashBankCode = LedgerAccounts.CashBank;
    public const string ArCommissionsCode = LedgerAccounts.Receivable;
    public const string ClearingCode = LedgerAccounts.Clearing;
    public const string CommissionRevenueCode = LedgerAccounts.CommissionRevenue;

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
        var defaults = LedgerAccounts.SystemAccounts;

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
        _logger.LogInformation("Ensured default chart of accounts ({Count} accounts)", defaults.Count);
    }

    public async Task EnsureUnit5BackfillAsync(ITenantDbContext context, CancellationToken ct = default)
    {
        var hasCounter = await context.FinanceCounters.AnyAsync(c => !c.IsDeleted, ct);
        if (!hasCounter)
        {
            context.FinanceCounters.Add(new FinanceCounter { NextJournalNumber = 1 });
            await context.SaveChangesAsync(ct);
        }

        await EnsureAccrualLedgerAsync(context, ct);
        _logger.LogDebug("Unit 5 finance backfill complete");
    }

    /// <summary>
    /// Ledgers written before fees were accrued booked every payment straight to revenue and
    /// never posted fees. Once per payment, reverse that revenue credit against the receivable;
    /// then post each commission's fees. Both steps are idempotent, so this runs on every start.
    /// </summary>
    private async Task EnsureAccrualLedgerAsync(ITenantDbContext context, CancellationToken ct)
    {
        var journal = new JournalPostingService(context, NullLogger<JournalPostingService>.Instance);

        var revenueId = await context.Accounts
            .Where(a => a.Code == LedgerAccounts.CommissionRevenue && !a.IsDeleted)
            .Select(a => a.Id)
            .FirstAsync(ct);

        var reclassified = await context.JournalEntries.AsNoTracking()
            .Where(j => !j.IsDeleted && j.SourceType == JournalPostingService.SourceTypeLegacyPaymentReclass)
            .Select(j => j.SourceId)
            .ToListAsync(ct);

        var legacyPayments = await context.JournalEntries.AsNoTracking()
            .Where(j => !j.IsDeleted
                && j.SourceType == JournalPostingService.SourceTypeCommissionPayment
                && j.Lines.Any(l => !l.IsDeleted && l.AccountId == revenueId && l.Credit > 0))
            .Select(j => new
            {
                j.SourceId,
                j.PostedAt,
                Amount = j.Lines.Where(l => !l.IsDeleted && l.AccountId == revenueId).Sum(l => l.Credit),
            })
            .ToListAsync(ct);

        var posted = 0;
        foreach (var legacy in legacyPayments.Where(p => !reclassified.Contains(p.SourceId)))
        {
            await journal.PostAsync(
                $"Reclassify payment {legacy.SourceId} from revenue to receivable",
                JournalPostingService.SourceTypeLegacyPaymentReclass,
                legacy.SourceId,
                [
                    new JournalLineSpec(LedgerAccounts.CommissionRevenue, legacy.Amount, 0),
                    new JournalLineSpec(LedgerAccounts.Receivable, 0, legacy.Amount),
                ],
                Guid.Empty,
                legacy.PostedAt,
                ct);
            posted++;
        }

        var commissions = await context.Commissions
            .Include(c => c.Fees)
            .Where(c => !c.IsDeleted && c.Fees.Any(f => !f.IsDeleted))
            .ToListAsync(ct);

        foreach (var commission in commissions)
        {
            if (await journal.SyncCommissionFeesAsync(commission, Guid.Empty, commission.OpenedAt, ct) is not null)
                posted++;
        }

        if (posted > 0)
        {
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Backfilled {Count} accrual journal entries", posted);
        }
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
