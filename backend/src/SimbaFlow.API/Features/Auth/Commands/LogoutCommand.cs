using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Auth.Commands;

// --- Command ---
public record LogoutCommand(string RefreshToken) : IRequest<Result>;

// --- Validator ---
public class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

// --- Handler ---
public class LogoutHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPlatformDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public LogoutHandler(
        IRefreshTokenService refreshTokenService,
        IPlatformDbContext context,
        ICurrentUserService currentUser)
    {
        _refreshTokenService = refreshTokenService;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Revoke the refresh token
        await _refreshTokenService.RevokeAsync(
            request.RefreshToken, _currentUser.IpAddress, "UserLogout", cancellationToken);

        // End the active session
        if (!string.IsNullOrEmpty(_currentUser.UserId))
        {
            var userId = Guid.Parse(_currentUser.UserId);
            var session = await _context.UserSessions
                .Where(s => s.UserId == userId && s.IsActive)
                .OrderByDescending(s => s.LoginAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (session is not null)
            {
                session.IsActive = false;
                session.LogoutAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return Result.Success();
    }
}
