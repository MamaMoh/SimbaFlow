using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
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

    public ResetPasswordHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

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

        // A self-chosen password also clears the forced-change flag from a temporary one.
        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            user.PasswordChangedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        return Result.Success();
    }
}
