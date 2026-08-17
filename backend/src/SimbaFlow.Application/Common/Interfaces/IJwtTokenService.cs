using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Generates and validates JWT access tokens.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>Generate a short-lived access token (15 min) with user claims, permissions, and roles.</summary>
    string GenerateAccessToken(ApplicationUser user, IReadOnlyList<string> permissions, IReadOnlyList<string> roles);

    /// <summary>
    /// Generate a limited, short-lived token that authenticates the user for MFA enrollment
    /// only. It carries no permissions, roles, tenant, or SuperAdmin claim, so permission-gated
    /// business endpoints reject it while the MFA setup/enable endpoints (auth-only) accept it.
    /// </summary>
    string GenerateMfaSetupToken(ApplicationUser user, int expiryMinutes);
}
