using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Auth.Commands;

/// <summary>
/// Turns MFA off for the current user. Requires the account password (re-auth) so a
/// hijacked, already-authenticated session cannot silently remove the second factor.
/// </summary>
public record DisableMfaCommand(string Password) : IRequest<Result>;

public class DisableMfaValidator : AbstractValidator<DisableMfaCommand>
{
    public DisableMfaValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class DisableMfaHandler : IRequestHandler<DisableMfaCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;

    public DisableMfaHandler(UserManager<ApplicationUser> userManager, ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DisableMfaCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
            return Result.Failure("Not authenticated", 401);

        var user = await _userManager.FindByIdAsync(_currentUser.UserId);
        if (user is null)
            return Result.Failure("User not found", 404);

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
            return Result.Failure("Invalid credentials", 401);

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        await _userManager.UpdateAsync(user);

        return Result.Success();
    }
}
