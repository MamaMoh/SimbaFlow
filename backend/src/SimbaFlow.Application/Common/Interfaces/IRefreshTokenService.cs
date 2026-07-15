using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Manages refresh token lifecycle: creation, rotation, revocation, theft detection.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Create a new refresh token for the user (7-day expiry).</summary>
    Task<(RefreshToken token, string rawValue)> CreateAsync(Guid userId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Rotate a refresh token: revoke old, issue new.
    /// Returns the new token and whether theft was detected (revoked token reused).
    /// </summary>
    Task<(RefreshToken newToken, string rawValue, bool isTheftDetected)> RotateAsync(string rawToken, string? ipAddress, CancellationToken ct = default);

    /// <summary>Revoke a specific refresh token.</summary>
    Task RevokeAsync(string rawToken, string? ipAddress, string reason, CancellationToken ct = default);

    /// <summary>Revoke ALL refresh tokens for a user (force logout everywhere).</summary>
    Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default);

    /// <summary>Remove expired tokens older than the specified age.</summary>
    Task CleanupExpiredAsync(int olderThanDays = 30, CancellationToken ct = default);
}
