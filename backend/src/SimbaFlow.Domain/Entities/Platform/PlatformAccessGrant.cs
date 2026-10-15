using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Platform;

/// <summary>
/// A platform administrator's stated reason for working inside one agency, and how long it lasts.
///
/// The API only honours the agency switch while one of these is live for that administrator and
/// agency, so looking at an agency's data always has a reason on record and runs out on its own.
/// The agency's owner can see each one.
/// </summary>
public class PlatformAccessGrant : BaseEntity
{
    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set when the administrator leaves the agency before it runs out.</summary>
    public DateTime? EndedAt { get; set; }
}
