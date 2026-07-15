using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.Infrastructure.Identity;

/// <summary>
/// Custom password validator that prevents reuse of recent passwords.
/// Checks new password hash against the last N entries in PasswordHistory.
/// </summary>
public class PasswordHistoryValidator : IPasswordValidator<ApplicationUser>
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public PasswordHistoryValidator(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<IdentityResult> ValidateAsync(
        UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (string.IsNullOrEmpty(password))
            return IdentityResult.Success;

        var historyCount = int.Parse(_configuration["Identity:Password:PasswordHistoryCount"] ?? "5");

        var recentHashes = await _context.PasswordHistories
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.CreatedAt)
            .Take(historyCount)
            .Select(h => h.PasswordHash)
            .ToListAsync();

        // Check the new password against each recent hash
        var hasher = manager.PasswordHasher;
        foreach (var oldHash in recentHashes)
        {
            var result = hasher.VerifyHashedPassword(user, oldHash, password);
            if (result != PasswordVerificationResult.Failed)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "PasswordRecentlyUsed",
                    Description = $"Cannot reuse any of the last {historyCount} passwords."
                });
            }
        }

        return IdentityResult.Success;
    }
}
