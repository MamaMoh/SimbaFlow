namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// High-throughput read audit for clinical data access (HIPAA compliance).
/// Uses async channels — does NOT block the HTTP thread.
/// </summary>
public interface IReadAuditService
{
    /// <summary>
    /// Publish a read-access event. Non-blocking, fire-and-forget to a channel.
    /// </summary>
    void LogAccess(ReadAuditEntry entry);
}

/// <summary>
/// Represents a clinical data read-access event.
/// </summary>
public record ReadAuditEntry(
    string? UserId,
    string EntityType,
    string? EntityId,
    string Action,
    string? IpAddress = null,
    string? UserAgent = null,
    bool IsBreakTheGlass = false,
    string? Reason = null);
