using Microsoft.AspNetCore.Identity;
using SimbaFlow.Domain.Entities.Staff;

namespace SimbaFlow.Domain.Entities.Identity;

/// <summary>
/// The security/authentication context for a system user.
/// Analogous to Epic's "EMP" (Employee/User Master).
/// 
/// Strictly handles: authentication, MFA, password policies,
/// IP restrictions, and IT role-based access control (RBAC).
/// 
/// Clinical/resource context lives in StaffProfile (linked via optional 1:1).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? ProfileImageUrl { get; set; }

    // ──── Account Status ────
    public bool IsActive { get; set; } = true;
    public bool IsFirstLogin { get; set; } = true;
    public bool IsSuperAdmin { get; set; }
    public bool MustChangePassword { get; set; }

    // ──── Security Policies (per-user overrides) ────
    /// <summary>
    /// Overrides the global MFA policy for this specific account.
    /// </summary>
    public bool RequireMfa { get; set; }

    /// <summary>
    /// CIDR blocks restricting where this user can authenticate from.
    /// Null/empty means no IP restriction (global policy applies).
    /// Example: ["10.0.0.0/8", "192.168.1.0/24"] for intranet-only billing clerks.
    /// </summary>
    public string[]? AllowedIpRanges { get; set; }

    // ──── Location Context ────
    public Guid? ActiveLocationId { get; set; }

    // ──── Organization ────
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // ──── Multi-tenancy ────
    public Guid? TenantId { get; set; }

    // ──── Office Context (Labour Export) ────
    public Guid? OfficeId { get; set; }

    // ──── Language Preference ────
    public string PreferredLanguage { get; set; } = "en";

    // ──── Bot Integration ────
    public bool BotLinked { get; set; }
    public string? TelegramChatId { get; set; }
    public string? WhatsAppNumber { get; set; }

    // ──── Activity Tracking ────
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public DateTime? PasswordExpiresAt { get; set; }

    // ──── Audit ────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }

    // ──── Navigation ────
    public StaffProfile? StaffProfile { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<UserSession> UserSessions { get; set; } = [];
    public ICollection<PasswordHistory> PasswordHistories { get; set; } = [];

    // ──── Computed ────
    public string FullName => string.IsNullOrEmpty(MiddleName)
        ? $"{FirstName} {LastName}"
        : $"{FirstName} {MiddleName} {LastName}";
}
