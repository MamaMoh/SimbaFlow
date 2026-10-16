namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Writes immutable audit log entries for compliance (HIPAA, GDPR).
/// </summary>
public interface IAuditService
{
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Whether anything was written through this instance — one per web request — so the
    /// catch-all for writes that skip MediatR does not record the same request twice.
    /// </summary>
    bool HasLogged => false;
}

/// <summary>
/// Represents a single audit trail entry.
/// </summary>
public record AuditEntry(
    string? UserId,
    string Action,
    string? EntityType,
    string? EntityId,
    bool Success,
    int DurationMs,
    string? OldValues = null,
    string? NewValues = null,
    string? IpAddress = null,
    string? UserAgent = null,
    Guid? TenantId = null);
