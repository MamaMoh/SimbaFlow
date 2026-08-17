using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Auth.Commands;

// --- Command ---
public record VerifyMfaCommand(string Username, string Password, string Code) : IRequest<Result<LoginResponse>>;

// --- Validator ---
public class VerifyMfaValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}

// --- Handler ---
public class VerifyMfaHandler : IRequestHandler<VerifyMfaCommand, Result<LoginResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPlatformDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public VerifyMfaHandler(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IPlatformDbContext context,
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<LoginResponse>> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(request.Username);
        if (user is null || !user.IsActive || user.IsDeleted)
            return Result<LoginResponse>.Failure("Invalid request", 401);

        // Lockout applies to the MFA step too (rate-limits code brute-forcing).
        if (await _userManager.IsLockedOutAsync(user))
            return Result<LoginResponse>.Failure("Account is locked. Try again later.", 423);

        // SECURITY: MFA is a SECOND factor — re-verify the password so a TOTP code alone
        // can never yield a session. Binds this step to a successful password check.
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return Result<LoginResponse>.Failure("Invalid credentials", 401);
        }

        // Only meaningful when the user actually enrolled MFA.
        if (!user.TwoFactorEnabled)
            return Result<LoginResponse>.Failure("MFA is not enabled for this account", 400);

        // Verify TOTP code
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, request.Code);

        if (!isValid)
        {
            await _userManager.AccessFailedAsync(user);
            return Result<LoginResponse>.Failure("Invalid verification code", 401);
        }

        // Complete login flow (same as successful login)
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

        var accessToken = _jwtTokenService.GenerateAccessToken(user, permissions, roles);
        var (refreshToken, rawRefreshToken) = await _refreshTokenService.CreateAsync(
            user.Id, _currentUser.IpAddress, cancellationToken);

        _context.UserSessions.Add(new UserSession
        {
            UserId = user.Id,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            RefreshTokenId = refreshToken.Id,
        });

        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = _currentUser.IpAddress;
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.UpdateAsync(user);
        await _context.SaveChangesAsync(cancellationToken);

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeMilliseconds();

        return Result<LoginResponse>.Success(new LoginResponse(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            ExpiresAt: expiresAt,
            User: new UserProfileDto(
                user.Id, user.UserName!, user.FullName, user.Email!,
                user.PhoneNumber, user.ProfileImageUrl,
                user.IsFirstLogin, user.IsSuperAdmin, user.DepartmentId,
                permissions, roles)));
    }
}
