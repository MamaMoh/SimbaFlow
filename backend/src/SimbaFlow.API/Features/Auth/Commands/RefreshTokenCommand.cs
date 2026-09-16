using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Auth.Commands;

// --- Command ---
public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<RefreshResponse>>;

// --- Response ---
/// <summary>
/// The rotated token pair, and the profile that goes with it.
///
/// The profile is included so a change to who the user is — their agency renamed, a role added —
/// reaches the browser within the fifteen minutes an access token lives, instead of waiting for
/// them to sign out and back in. Roles and permissions are already loaded here to mint the token;
/// the profile costs one more small read.
/// </summary>
public record RefreshResponse(
    string AccessToken,
    string RefreshToken,
    long ExpiresAt,
    UserProfileDto? User = null);

// --- Validator ---
public class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

// --- Handler ---
public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, Result<RefreshResponse>>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPlatformDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RefreshTokenHandler(
        IRefreshTokenService refreshTokenService,
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IPlatformDbContext context,
        ICurrentUserService currentUser)
    {
        _refreshTokenService = refreshTokenService;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<RefreshResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // Rotate the refresh token (handles theft detection)
        var (newToken, rawNewToken, isTheft) = await _refreshTokenService.RotateAsync(
            request.RefreshToken, _currentUser.IpAddress, cancellationToken);

        if (isTheft)
            return Result<RefreshResponse>.Failure("Session compromised. All sessions terminated. Please login again.", 401);

        // Load user with fresh data
        var user = await _userManager.FindByIdAsync(newToken.UserId.ToString());
        if (user is null || !user.IsActive || user.IsDeleted)
            return Result<RefreshResponse>.Failure("User not found or inactive", 401);

        // An agency suspended while its staff were working should not keep them working. Access
        // tokens last fifteen minutes, so checking here is what turns a suspension into an actual
        // sign-out rather than a slow puncture — and reactivating lets them straight back in.
        if (await TenantSignInGuard.RefusalFor(user, _context, cancellationToken) is string refusal)
            return Result<RefreshResponse>.Failure(refusal, 403);

        // Get fresh roles and permissions
        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        var permissions = await _context.RolePermissions
            .Include(rp => rp.Permission)
            .Include(rp => rp.Role)
            .Where(rp => roles.Contains(rp.Role.Name!))
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (user.IsSuperAdmin)
            permissions = ["system.admin", .. permissions];

        // Generate new access token
        var accessToken = _jwtTokenService.GenerateAccessToken(user, permissions, roles);

        // Update the activity of the session this token actually belongs to.
        //
        // Picking the most recently created session instead meant that, with two devices signed in,
        // refreshing on the phone rewrote the laptop's row: the session list showed the wrong device
        // as active, and SessionCleanupService reaped the wrong one. The presented token is found
        // through the link the rotation leaves behind — the old token records the hash of the one
        // that replaced it.
        var previousTokenId = await _context.RefreshTokens
            .Where(t => t.ReplacedByTokenHash == newToken.TokenHash)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var session = previousTokenId is Guid previousId
            ? await _context.UserSessions.FirstOrDefaultAsync(
                s => s.UserId == user.Id && s.IsActive && s.RefreshTokenId == previousId,
                cancellationToken)
            : null;

        // When the session cannot be identified — a concurrent refresh is issued a fresh token with
        // no predecessor — nothing is touched. Leaving a row slightly stale is better than writing
        // the wrong device's.
        if (session is not null)
        {
            session.LastActivityAt = DateTime.UtcNow;
            session.RefreshTokenId = newToken.Id;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeMilliseconds();

        var profile = await SignedInProfile.BuildAsync(user, permissions, roles, _context, cancellationToken);

        return Result<RefreshResponse>.Success(
            new RefreshResponse(accessToken, rawNewToken, expiresAt, profile));
    }
}
