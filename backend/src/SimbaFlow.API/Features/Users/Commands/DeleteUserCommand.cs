using MediatR;
using Microsoft.AspNetCore.Identity;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Users.Commands;

public record DeleteUserCommand(Guid Id) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "users.write";
}

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly IRefreshTokenService _refreshTokenService;

    public DeleteUserHandler(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        IRefreshTokenService refreshTokenService)
    {
        _userManager = userManager;
        _currentUser = currentUser;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.Id.ToString());
        if (user is null)
            return Result.Failure("User not found", 404);

        // Prevent self-deletion
        if (_currentUser.UserId == user.Id.ToString())
            return Result.Failure("Cannot delete your own account", 400);

        // Soft delete
        user.IsDeleted = true;
        user.IsActive = false;
        await _userManager.UpdateAsync(user);

        // Revoke all sessions
        await _refreshTokenService.RevokeAllForUserAsync(user.Id, "AccountDeleted", cancellationToken);

        return Result.Success(204);
    }
}
