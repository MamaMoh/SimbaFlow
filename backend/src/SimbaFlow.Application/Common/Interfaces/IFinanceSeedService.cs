using SimbaFlow.Domain.Entities.Finance;

namespace SimbaFlow.Application.Common.Interfaces;

public interface IFinanceSeedService
{
    Task EnsureDefaultChartOfAccountsAsync(ITenantDbContext context, CancellationToken ct = default);

    /// <summary>
    /// Ensures finance counter row exists and commission totals default to 0 where needed.
    /// </summary>
    Task EnsureUnit5BackfillAsync(ITenantDbContext context, CancellationToken ct = default);

    Task EnsureUnit5ArtifactsIntoSchemaAsync(
        string connectionString,
        string schemaName,
        Guid tenantId,
        CancellationToken ct = default);
}
