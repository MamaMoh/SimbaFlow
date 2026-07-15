using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Auth.Commands;

// --- Command ---
public record VerifyMfaCommand(string Username, string Code) : IRequest<Result<LoginResponse>>;

// --- Validator ---
public class VerifyMfaValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}

// --- Handler ---
public class VerifyMfaHandler : IRequestHandler<VerifyMfaCommand, Result<LoginResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public VerifyMfaHandler(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IApplicationDbContext context,
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

        // Verify TOTP code
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, request.Code);

        if (!isValid)
            return Result<LoginResponse>.Failure("Invalid verification code", 401);

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
