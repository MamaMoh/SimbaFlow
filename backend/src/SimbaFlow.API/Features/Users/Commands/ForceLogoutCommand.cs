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
    private readonly IApplicationDbContext _context;

    public ForceLogoutHandler(IRefreshTokenService refreshTokenService, IApplicationDbContext context)
    {
        _refreshTokenService = refreshTokenService;
        _context = context;
    }

    public async Task<Result> Handle(ForceLogoutCommand request, CancellationToken cancellationToken)
    {
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
