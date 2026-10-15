using System.Text.Json;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Features.Platform;

/// <summary>
/// Writes the platform team's own actions to the audit log, for the endpoints that do not go
/// through MediatR (and so miss the audit behaviour).
///
/// The row carries the agency it was about as its tenant, so it shows on that agency's audit
/// page as well as on the platform-wide one.
/// </summary>
public static class PlatformAudit
{
    public static Task LogAsync(
        IAuditService audit,
        ICurrentUserService user,
        string action,
        Guid? tenantId,
        string? entityType = null,
        string? entityId = null,
        object? details = null,
        CancellationToken ct = default) =>
        audit.LogAsync(new AuditEntry(
            UserId: user.UserId,
            Action: action,
            EntityType: entityType,
            EntityId: entityId,
            Success: true,
            DurationMs: 0,
            NewValues: details is null ? null : JsonSerializer.Serialize(details),
            IpAddress: user.IpAddress,
            UserAgent: user.UserAgent,
            TenantId: tenantId), ct);
}
