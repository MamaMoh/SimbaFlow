using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Generates and validates JWT access tokens.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>Generate a short-lived access token (15 min) with user claims, permissions, and roles.</summary>
    string GenerateAccessToken(ApplicationUser user, IReadOnlyList<string> permissions, IReadOnlyList<string> roles);
}
