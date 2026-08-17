using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Auth.Commands;

// --- Command ---
public record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword) : IRequest<Result>;

// --- Validator ---
public class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain a digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a special character")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from current password");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match");
    }
}

// --- Handler ---
public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPlatformDbContext _context;

    public ChangePasswordHandler(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        IRefreshTokenService refreshTokenService,
        IPlatformDbContext context)
    {
        _userManager = userManager;
        _currentUser = currentUser;
        _refreshTokenService = refreshTokenService;
        _context = context;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
            return Result.Failure("Not authenticated", 401);

        var user = await _userManager.FindByIdAsync(_currentUser.UserId);
        if (user is null)
            return Result.Failure("User not found", 404);

        // Record current password hash in history BEFORE change
        _context.PasswordHistories.Add(new PasswordHistory
        {
            UserId = user.Id,
            PasswordHash = user.PasswordHash!,
        });

        // Change password (triggers PasswordHistoryValidator)
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result.Failure(errors, 400);
        }

        // Update password metadata
        user.IsFirstLogin = false;
        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTime.UtcNow;
        user.PasswordExpiresAt = DateTime.UtcNow.AddDays(90);
        await _userManager.UpdateAsync(user);

        await _context.SaveChangesAsync(cancellationToken);

        // Revoke all refresh tokens (force re-login everywhere)
        await _refreshTokenService.RevokeAllForUserAsync(user.Id, "PasswordChanged", cancellationToken);

        return Result.Success();
    }
}
