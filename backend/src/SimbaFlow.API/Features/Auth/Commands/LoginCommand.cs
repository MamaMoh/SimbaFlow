using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Infrastructure.Options;

namespace SimbaFlow.API.Features.Auth.Commands;

// --- Command ---
public record LoginCommand(string Username, string Password) : IRequest<Result<LoginResponse>>;

// --- Response DTOs ---
public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    long ExpiresAt,
    UserProfileDto User,
    bool RequiresPasswordChange = false,
    bool RequiresMfa = false,
    bool RequiresMfaSetup = false);

public record UserProfileDto(
    Guid Id,
    string Username,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? ProfileImageUrl,
    bool IsFirstLogin,
    bool IsSuperAdmin,
    Guid? DepartmentId,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Roles);

// --- Validator ---
public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

// --- Handler ---
public class LoginHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPlatformDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly MfaOptions _mfa;

    public LoginHandler(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IPlatformDbContext context,
        ICurrentUserService currentUser,
        IOptions<MfaOptions> mfaOptions)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _context = context;
        _currentUser = currentUser;
        _mfa = mfaOptions.Value;
    }

    /// <summary>True when this user's roles require MFA per policy.</summary>
    private bool MfaRequiredFor(ApplicationUser user, IReadOnlyList<string> roles) =>
        _mfa.Enforce && (
            (_mfa.RequireForSuperAdmin && user.IsSuperAdmin) ||
            roles.Any(r => _mfa.RequiredRoles.Contains(r, StringComparer.OrdinalIgnoreCase)));

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Find user (ignore query filters to handle soft-deleted check manually)
        var user = await _userManager.FindByNameAsync(request.Username);

        if (user is null)
            return Result<LoginResponse>.Failure("Invalid credentials", 401);

        // Check soft delete
        if (user.IsDeleted)
            return Result<LoginResponse>.Failure("Invalid credentials", 401);

        // Check active
        if (!user.IsActive)
            return Result<LoginResponse>.Failure("Account is deactivated", 403);

        // Check lockout
        if (await _userManager.IsLockedOutAsync(user))
            return Result<LoginResponse>.Failure("Account is locked. Try again later.", 423);

        // Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return Result<LoginResponse>.Failure("Invalid credentials", 401);
        }

        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        // Check if MFA is enabled → challenge for the TOTP code (second factor).
        if (user.TwoFactorEnabled)
        {
            // Return MFA required response (frontend will prompt for TOTP code)
            return Result<LoginResponse>.Success(new LoginResponse(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: 0,
                User: null!,
                RequiresPasswordChange: false,
                RequiresMfa: true));
        }

        // Enforcement: a privileged user without MFA must enroll before receiving a
        // full token. Issue only a limited setup token scoped to the enrollment endpoints.
        if (MfaRequiredFor(user, roles))
        {
            var setupToken = _jwtTokenService.GenerateMfaSetupToken(user, _mfa.SetupTokenMinutes);
            var setupExpiry = DateTimeOffset.UtcNow.AddMinutes(_mfa.SetupTokenMinutes).ToUnixTimeMilliseconds();
            return Result<LoginResponse>.Success(new LoginResponse(
                AccessToken: setupToken,
                RefreshToken: string.Empty,
                ExpiresAt: setupExpiry,
                User: new UserProfileDto(
                    user.Id, user.UserName!, user.FullName, user.Email!,
                    user.PhoneNumber, user.ProfileImageUrl,
                    user.IsFirstLogin, user.IsSuperAdmin, user.DepartmentId,
                    [], roles),
                RequiresMfaSetup: true));
        }

        // Generate tokens and complete login
        return await CompleteLoginAsync(user, roles, cancellationToken);
    }

    private async Task<Result<LoginResponse>> CompleteLoginAsync(
        ApplicationUser user, List<string> roles, CancellationToken ct)
    {
        var permissions = await _context.RolePermissions
            .Include(rp => rp.Permission)
            .Include(rp => rp.Role)
            .Where(rp => roles.Contains(rp.Role.Name!))
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(ct);

        // Add SuperAdmin implicit permissions
        if (user.IsSuperAdmin)
            permissions = ["system.admin", .. permissions];

        // Generate access token
        var accessToken = _jwtTokenService.GenerateAccessToken(user, permissions, roles);

        // Generate refresh token
        var (refreshToken, rawRefreshToken) = await _refreshTokenService.CreateAsync(
            user.Id, _currentUser.IpAddress, ct);

        // Create session
        _context.UserSessions.Add(new UserSession
        {
            UserId = user.Id,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            RefreshTokenId = refreshToken.Id,
        });

        // Update login info
        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = _currentUser.IpAddress;
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.UpdateAsync(user);

        await _context.SaveChangesAsync(ct);

        // Check password expiry
        var requiresPasswordChange = user.IsFirstLogin || user.MustChangePassword ||
            (user.PasswordExpiresAt.HasValue && user.PasswordExpiresAt < DateTime.UtcNow);

        // Calculate token expiry
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeMilliseconds();

        return Result<LoginResponse>.Success(new LoginResponse(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            ExpiresAt: expiresAt,
            User: new UserProfileDto(
                user.Id, user.UserName!, user.FullName, user.Email!,
                user.PhoneNumber, user.ProfileImageUrl,
                user.IsFirstLogin, user.IsSuperAdmin, user.DepartmentId,
                permissions, roles),
            RequiresPasswordChange: requiresPasswordChange));
    }
}
