using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Auth.Commands;

public record ResetPasswordCommand(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmPassword) : IRequest<Result>;

public class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain a digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a special character");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match");
    }
}

public class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPlatformDbContext _context;

    public ResetPasswordHandler(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenService refreshTokenService,
        IPlatformDbContext context)
    {
        _userManager = userManager;
        _refreshTokenService = refreshTokenService;
        _context = context;
    }

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.IsDeleted)
            return Result.Failure("This reset link is not valid any more. Request a new one.", 400);

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return Result.Failure("This reset link is not valid any more. Request a new one.", 400);
        }

        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            var invalidToken = result.Errors.Any(e => e.Code.Contains("Token", StringComparison.OrdinalIgnoreCase));
            return Result.Failure(
                invalidToken
                    ? "This reset link has expired or was already used. Request a new one."
                    : string.Join(" ", result.Errors.Select(e => e.Description)),
                400);
        }

        // A reset is also the way out of a lockout — otherwise the user proves who they are and
        // is still refused until the lockout window expires.
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        // The password they just chose is their own, so nothing further is owed.
        //
        // Login treats IsFirstLogin, MustChangePassword and an elapsed expiry as equal grounds to
        // demand a change, so clearing only one of them sent the user straight from a successful
        // reset to the change-password screen — having just set the password it was asking for.
        user.MustChangePassword = false;
        user.IsFirstLogin = false;
        user.PasswordChangedAt = DateTime.UtcNow;
        user.PasswordExpiresAt = DateTime.UtcNow.AddDays(90);
        await _userManager.UpdateAsync(user);

        // End every existing session, as changing a password does.
        //
        // This matters more here than there: a reset is the path someone takes precisely because
        // they believe their account is compromised. Refresh tokens last seven days, so without
        // this an attacker holding one keeps renewing a session straight through the reset — the
        // user does the one thing they know to do and it changes nothing.
        await _refreshTokenService.RevokeAllForUserAsync(user.Id, "PasswordReset", ct);

        var sessions = await _context.UserSessions
            .Where(s => s.UserId == user.Id && s.IsActive)
            .ToListAsync(ct);
        foreach (var session in sessions)
        {
            session.IsActive = false;
            session.LogoutAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
