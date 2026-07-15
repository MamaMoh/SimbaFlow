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

    public async Task<(RefreshToken newToken, string rawValue, bool isTheftDetected)> RotateAsync(
        string rawToken, string? ipAddress, CancellationToken ct = default)
    {
        var tokenHash = HashToken(rawToken);

        var existingToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (existingToken is null)
            throw new UnauthorizedAccessException("Invalid refresh token");

        // THEFT DETECTION: If token is already revoked, someone is reusing a stolen token
        if (existingToken.IsRevoked)
        {
            _logger.LogError(
                "TOKEN THEFT DETECTED: UserId={UserId}, RevokedTokenReused, IP={IpAddress}",
                existingToken.UserId, ipAddress);

            // Revoke ALL tokens for this user (nuclear option)
            await RevokeAllForUserAsync(existingToken.UserId, "TokenTheftDetected", ct);

            return (existingToken, string.Empty, true);
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
