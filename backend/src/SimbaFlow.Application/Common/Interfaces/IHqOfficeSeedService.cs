using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Idempotent HQ branch (Department) seed for a newly provisioned tenant.
/// </summary>
public interface IHqOfficeSeedService
{
    /// <summary>
    /// Creates a default "Head Office" / HQ department when the tenant has zero offices.
    /// </summary>
    Task EnsureDefaultHqOfficeAsync(
        Guid tenantId,
        string? address,
        string? city,
        string? country,
        CancellationToken ct = default);
}
