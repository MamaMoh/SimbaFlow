using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Users.Commands;

public record ForceLogoutCommand(Guid UserId) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "users.write";
}

public class ForceLogoutHandler : IRequestHandler<ForceLogoutCommand, Result>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPlatformDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ForceLogoutHandler(
        IRefreshTokenService refreshTokenService,
        IPlatformDbContext context,
        ICurrentUserService currentUser)
    {
        _refreshTokenService = refreshTokenService;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ForceLogoutCommand request, CancellationToken cancellationToken)
    {
        // SECURITY: tenant admins may only force-logout users in their own tenant.
        var target = await _context.ApplicationUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (target is null)
            return Result.Failure("User not found", 404);
        if (!UserAccessGuard.CanManage(_currentUser, target))
            return Result.Failure("User not found", 404);

        // Revoke all refresh tokens
        await _refreshTokenService.RevokeAllForUserAsync(request.UserId, "AdminForceLogout", cancellationToken);

        // End all active sessions
        var activeSessions = await _context.UserSessions
            .Where(s => s.UserId == request.UserId && s.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            session.LogoutAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
