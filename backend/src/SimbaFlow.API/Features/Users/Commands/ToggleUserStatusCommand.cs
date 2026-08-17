using MediatR;
using Microsoft.AspNetCore.Identity;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Users.Commands;

public record ToggleUserStatusCommand(Guid UserId) : IRequest<Result<bool>>, IRequirePermission
{
    public string RequiredPermission => "users.write";
}

public class ToggleUserStatusHandler : IRequestHandler<ToggleUserStatusCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ICurrentUserService _currentUser;

    public ToggleUserStatusHandler(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenService refreshTokenService,
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _refreshTokenService = refreshTokenService;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(ToggleUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<bool>.Failure("User not found", 404);

        // SECURITY: tenant admins may only enable/disable users in their own tenant.
        if (!UserAccessGuard.CanManage(_currentUser, user))
            return Result<bool>.Failure("User not found", 404);

        // Prevent self-lockout.
        if (_currentUser.UserId == user.Id.ToString())
            return Result<bool>.Failure("Cannot change your own account status", 400);

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        // If deactivated, revoke all sessions
        if (!user.IsActive)
            await _refreshTokenService.RevokeAllForUserAsync(user.Id, "AccountDeactivated", cancellationToken);

        return Result<bool>.Success(user.IsActive);
    }
}
