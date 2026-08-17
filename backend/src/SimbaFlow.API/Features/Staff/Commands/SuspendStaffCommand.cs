using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Staff;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Staff.Commands;

/// <summary>
/// Suspends a staff member pending investigation.
/// Unlike termination, this is reversible. Effects:
/// 1. Blocks login immediately (ApplicationUser.IsActive = false)
/// 2. Freezes clinical privileges (does NOT end affiliations)
/// 3. Revokes active tokens
/// 4. Raises StaffSuspendedEvent for compliance notification
/// </summary>
public record SuspendStaffCommand(
    Guid StaffProfileId,
    string Reason) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "staff.suspend";
}

// --- Validator ---
public class SuspendStaffValidator : AbstractValidator<SuspendStaffCommand>
{
    public SuspendStaffValidator()
    {
        RuleFor(x => x.StaffProfileId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

// --- Handler ---
public class SuspendStaffHandler : IRequestHandler<SuspendStaffCommand, Result>
{
    private readonly IPlatformDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SuspendStaffHandler(IPlatformDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(SuspendStaffCommand request, CancellationToken cancellationToken)
    {
        var staffProfile = await _context.StaffProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StaffProfileId, cancellationToken);

        if (staffProfile is null)
            return Result.Failure("Staff profile not found", 404);

        if (staffProfile.EmploymentStatus == EmploymentStatus.Suspended)
            return Result.Failure("Staff member is already suspended", 409);

        if (staffProfile.EmploymentStatus == EmploymentStatus.Terminated)
            return Result.Failure("Cannot suspend a terminated staff member", 409);

        // 1. Update employment status
        staffProfile.EmploymentStatus = EmploymentStatus.Suspended;
        staffProfile.IsActive = false;

        // 2. Block login and revoke tokens (if linked user exists)
        if (staffProfile.UserId.HasValue)
        {
            var user = await _context.ApplicationUsers
                .FirstOrDefaultAsync(u => u.Id == staffProfile.UserId.Value, cancellationToken);

            if (user is not null)
            {
                user.IsActive = false;

                // Revoke active refresh tokens
                var activeTokens = await _context.RefreshTokens
                    .Where(t => t.UserId == user.Id && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync(cancellationToken);

                foreach (var token in activeTokens)
                {
                    token.RevokedAt = DateTime.UtcNow;
                    token.ReasonRevoked = $"Staff suspended: {request.Reason}";
                }

                // Kill active sessions
                var activeSessions = await _context.UserSessions
                    .Where(s => s.UserId == user.Id && s.IsActive)
                    .ToListAsync(cancellationToken);

                foreach (var session in activeSessions)
                {
                    session.IsActive = false;
                    session.LogoutAt = DateTime.UtcNow;
                }
            }
        }

        // 3. Raise domain event
        staffProfile.AddDomainEvent(new StaffSuspendedEvent(
            staffProfile.Id,
            staffProfile.UserId,
            _currentUserService.UserId,
            request.Reason));

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
