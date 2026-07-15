using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Identity;

/// <summary>
/// Tracks user login sessions for activity logging and security auditing.
/// Supports impersonation tracking for Tier 3 support scenarios.
/// </summary>
public class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public DateTime LoginAt { get; set; } = DateTime.UtcNow;
    public DateTime? LogoutAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    // Link to refresh token for this session
    public Guid? RefreshTokenId { get; set; }
    public RefreshToken? RefreshToken { get; set; }

    // ──── Impersonation ────
    /// <summary>
    /// If this session is impersonated, the ID of the admin who initiated it.
    /// Null for normal sessions. When set, audit logs MUST record both actors.
    /// </summary>
    public Guid? ImpersonatedByUserId { get; set; }
    public ApplicationUser? ImpersonatedByUser { get; set; }

    /// <summary>Why the impersonation was initiated.</summary>
    public ImpersonationReason? ImpersonationReason { get; set; }

    /// <summary>Free-text justification (required for audit compliance).</summary>
    public string? ImpersonationJustification { get; set; }

    /// <summary>Whether this is an impersonated session.</summary>
    public bool IsImpersonated => ImpersonatedByUserId.HasValue;
}
