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
public record RefreshResponse(string AccessToken, string RefreshToken, long ExpiresAt);

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
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RefreshTokenHandler(
        IRefreshTokenService refreshTokenService,
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IApplicationDbContext context,
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

        // Update session activity
        var session = await _context.UserSessions
            .Where(s => s.UserId == user.Id && s.IsActive)
            .OrderByDescending(s => s.LoginAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is not null)
        {
            session.LastActivityAt = DateTime.UtcNow;
            session.RefreshTokenId = newToken.Id;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeMilliseconds();

        return Result<RefreshResponse>.Success(new RefreshResponse(accessToken, rawNewToken, expiresAt));
    }
}
