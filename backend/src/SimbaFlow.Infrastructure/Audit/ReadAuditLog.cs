namespace SimbaFlow.Infrastructure.Audit;

/// <summary>
/// Separate table for read-access audit logs.
/// High-volume, append-only. Designed for monthly partitioning in PostgreSQL.
/// </summary>
public class ReadAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; }

    /// <summary>If impersonated, the admin who initiated the session.</summary>
    public string? ImpersonatedByUserId { get; set; }

    /// <summary>StaffProfile ID for clinical audit correlation.</summary>
    public string? StaffProfileId { get; set; }

    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsBreakTheGlass { get; set; }
    public string? Reason { get; set; }
}
