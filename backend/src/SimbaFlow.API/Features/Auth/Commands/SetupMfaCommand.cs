using MediatR;
using Microsoft.AspNetCore.Identity;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Auth.Commands;

// --- Command ---
public record SetupMfaCommand : IRequest<Result<MfaSetupResponse>>;

// --- Response ---
public record MfaSetupResponse(string SharedKey, string QrCodeUri);

// --- Handler ---
public class SetupMfaHandler : IRequestHandler<SetupMfaCommand, Result<MfaSetupResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;

    public SetupMfaHandler(UserManager<ApplicationUser> userManager, ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task<Result<MfaSetupResponse>> Handle(SetupMfaCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
            return Result<MfaSetupResponse>.Failure("Not authenticated", 401);

        var user = await _userManager.FindByIdAsync(_currentUser.UserId);
        if (user is null)
            return Result<MfaSetupResponse>.Failure("User not found", 404);

        // Reset authenticator key to generate a new one
        await _userManager.ResetAuthenticatorKeyAsync(user);
        var key = await _userManager.GetAuthenticatorKeyAsync(user);

        if (string.IsNullOrEmpty(key))
            return Result<MfaSetupResponse>.Failure("Failed to generate authenticator key", 500);

        var email = user.Email ?? user.UserName ?? "user";
        var qrCodeUri = $"otpauth://totp/SimbaFlow:{email}?secret={key}&issuer=SimbaFlow";

        return Result<MfaSetupResponse>.Success(new MfaSetupResponse(key, qrCodeUri));
    }
}
