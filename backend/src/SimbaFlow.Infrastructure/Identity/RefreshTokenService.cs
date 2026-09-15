using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.Infrastructure.Identity;

/// <summary>
/// Manages refresh token lifecycle: creation, rotation with theft detection, revocation.
/// Tokens are stored as SHA-256 hashes; raw values returned to client only once.
/// </summary>
public class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<RefreshTokenService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(RefreshToken token, string rawValue)> CreateAsync(
        Guid userId, string? ipAddress, CancellationToken ct = default)
    {
        var expiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");
        var maxTokens = int.Parse(_configuration["Jwt:MaxActiveRefreshTokens"] ?? "5");

        // Enforce max active tokens per user
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        if (activeTokens.Count >= maxTokens)
        {
            // Revoke oldest tokens to make room
            var tokensToRevoke = activeTokens.Take(activeTokens.Count - maxTokens + 1);
            foreach (var oldToken in tokensToRevoke)
            {
                oldToken.RevokedAt = DateTime.UtcNow;
                oldToken.ReasonRevoked = "MaxTokensExceeded";
            }
        }

        var rawValue = GenerateSecureToken();
        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(rawValue),
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            CreatedByIp = ipAddress,
        };

        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync(ct);

        return (token, rawValue);
    }

    /// <summary>
    /// How long after a rotation the old token may still be presented without being read as theft.
    /// Long enough to cover parallel requests from one client, short enough that a replay has to
    /// be near-instant.
    /// </summary>
    private static readonly TimeSpan RotationGraceWindow = TimeSpan.FromSeconds(60);

    public async Task<(RefreshToken newToken, string rawValue, bool isTheftDetected)> RotateAsync(
        string rawToken, string? ipAddress, CancellationToken ct = default)
    {
        var tokenHash = HashToken(rawToken);

        var existingToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (existingToken is null)
            throw new UnauthorizedAccessException("Invalid refresh token");

        if (existingToken.IsRevoked)
        {
            // A token that was rotated a moment ago and is being presented again is almost
            // always two of the app's own requests refreshing at the same time, not a thief.
            // The session layer can run its refresh from several requests at once, and the
            // loser of that race arrives holding a token the winner has just rotated.
            //
            // Treating that as theft revoked every session the user had and threw them out of
            // the app mid-task, which is what was happening in production several times a
            // minute. Inside the grace window the request is served instead: the presented
            // token stays revoked, and the caller gets a fresh pair.
            //
            // A replayed stolen token still trips the alarm — it would have to be replayed
            // within seconds of the legitimate rotation to pass, and outside that window the
            // response is unchanged.
            var rotatedRecently =
                existingToken.ReasonRevoked == "Rotated"
                && existingToken.RevokedAt is DateTime revokedAt
                && DateTime.UtcNow - revokedAt < RotationGraceWindow;

            if (!rotatedRecently)
            {
                _logger.LogError(
                    "TOKEN THEFT DETECTED: UserId={UserId}, RevokedTokenReused, IP={IpAddress}",
                    existingToken.UserId, ipAddress);

                // Revoke ALL tokens for this user (nuclear option)
                await RevokeAllForUserAsync(existingToken.UserId, "TokenTheftDetected", ct);

                return (existingToken, string.Empty, true);
            }

            _logger.LogInformation(
                "Concurrent refresh for UserId={UserId} — token rotated {Age:0.0}s ago, issuing a fresh pair",
                existingToken.UserId,
                (DateTime.UtcNow - existingToken.RevokedAt!.Value).TotalSeconds);

            var (raceToken, raceRaw) = await CreateAsync(existingToken.UserId, ipAddress, ct);
            return (raceToken, raceRaw, false);
        }

        // Check expiry
        if (existingToken.IsExpired)
            throw new UnauthorizedAccessException("Refresh token expired");

        // Revoke the old token
        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.RevokedByIp = ipAddress;
        existingToken.ReasonRevoked = "Rotated";

        // Create new token
        var newRawValue = GenerateSecureToken();
        var newToken = new RefreshToken
        {
            UserId = existingToken.UserId,
            TokenHash = HashToken(newRawValue),
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7")),
            CreatedByIp = ipAddress,
        };

        existingToken.ReplacedByTokenHash = newToken.TokenHash;

        _context.RefreshTokens.Add(newToken);
        await _context.SaveChangesAsync(ct);

        return (newToken, newRawValue, false);
    }

    public async Task RevokeAsync(string rawToken, string? ipAddress, string reason, CancellationToken ct = default)
    {
        var tokenHash = HashToken(rawToken);
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (token is null || token.IsRevoked) return;

        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = ipAddress;
        token.ReasonRevoked = reason;

        await _context.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.ReasonRevoked = reason;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogWarning("Revoked all {Count} refresh tokens for UserId={UserId}. Reason: {Reason}",
            activeTokens.Count, userId, reason);
    }

    public async Task CleanupExpiredAsync(int olderThanDays = 30, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);

        var deleted = await _context.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            _logger.LogInformation("Cleaned up {Count} expired refresh tokens older than {Days} days",
                deleted, olderThanDays);
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
