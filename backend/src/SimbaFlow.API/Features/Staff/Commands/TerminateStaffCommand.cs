using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Staff.Commands;

/// <summary>
/// Critical enterprise event. Terminates a staff member, which:
/// 1. Sets EmploymentStatus = Terminated and records TerminationDate
/// 2. Deactivates their ApplicationUser (kills JWT/refresh tokens instantly)
/// 3. Ends all active department affiliations
/// 4. Raises StaffTerminatedEvent for downstream handlers (candidate reassignment, etc.)
/// 
/// This is NOT reversible without executive approval. Use SuspendStaffCommand for temporary blocks.
/// </summary>
public record TerminateStaffCommand(
    Guid StaffProfileId,
    string Reason,
    DateOnly TerminationDate) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "staff.terminate";
}

// --- Validator ---
public class TerminateStaffValidator : AbstractValidator<TerminateStaffCommand>
{
    public TerminateStaffValidator()
    {
        RuleFor(x => x.StaffProfileId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TerminationDate).NotEmpty();
    }
}

// --- Handler ---
public class TerminateStaffHandler : IRequestHandler<TerminateStaffCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public TerminateStaffHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(TerminateStaffCommand request, CancellationToken cancellationToken)
    {
        var staffProfile = await _context.StaffProfiles
            .Include(s => s.DepartmentAffiliations)
            .FirstOrDefaultAsync(s => s.Id == request.StaffProfileId, cancellationToken);

        if (staffProfile is null)
            return Result.Failure("Staff profile not found", 404);

        if (staffProfile.EmploymentStatus == EmploymentStatus.Terminated)
            return Result.Failure("Staff member is already terminated", 409);

        var previousStatus = staffProfile.EmploymentStatus;

        // 1. Update staff profile
        staffProfile.EmploymentStatus = EmploymentStatus.Terminated;
        staffProfile.TerminationDate = request.TerminationDate;
        staffProfile.IsActive = false;

        // 2. End all active and cancel future-dated department affiliations
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var affiliation in staffProfile.DepartmentAffiliations.Where(a => !a.IsDeleted))
        {
            if (affiliation.EffectiveStartDate > today)
            {
                // Future-dated: cancel entirely via soft delete
                affiliation.IsDeleted = true;
            }
            else if (affiliation.EffectiveEndDate == null || affiliation.EffectiveEndDate > today)
            {
                // Currently active or ending in future: end today
                affiliation.EffectiveEndDate = today;
            }
        }

        // 3. Deactivate linked user account (if exists)
        if (staffProfile.UserId.HasValue)
        {
            var user = await _context.ApplicationUsers
                .FirstOrDefaultAsync(u => u.Id == staffProfile.UserId.Value, cancellationToken);

            if (user is not null)
            {
                user.IsActive = false;

                // Revoke all active refresh tokens
                var activeTokens = await _context.RefreshTokens
                    .Where(t => t.UserId == user.Id && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync(cancellationToken);

                foreach (var token in activeTokens)
                {
                    token.RevokedAt = DateTime.UtcNow;
                    token.ReasonRevoked = $"Staff terminated: {request.Reason}";
                }

                // End all active sessions
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

        // 4. Raise domain event for downstream processing
        staffProfile.AddDomainEvent(new Domain.Entities.Staff.StaffTerminatedEvent(
            staffProfile.Id,
            staffProfile.UserId,
            previousStatus,
            _currentUserService.UserId,
            request.Reason));

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
