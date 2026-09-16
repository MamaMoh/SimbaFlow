using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Users.Commands;

public record AssignRolesCommand(Guid UserId, List<string>? RoleNames, List<Guid>? RoleIds)
    : IRequest<Result<bool>>, IRequirePermission
{
    public string RequiredPermission => "users.write";
}

public class AssignRolesValidator : AbstractValidator<AssignRolesCommand>
{
    public AssignRolesValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class AssignRolesHandler : IRequestHandler<AssignRolesCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IPlatformDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AssignRolesHandler(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IPlatformDbContext context,
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(AssignRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<bool>.Failure("User not found", 404);

        // SECURITY: tenant admins may only assign roles to users in their own tenant.
        if (!UserAccessGuard.CanManage(_currentUser, user))
            return Result<bool>.Failure("User not found", 404);

        // Resolve role names from RoleIds if provided
        var roleNamesToAssign = new List<string>();

        if (request.RoleIds is { Count: > 0 })
        {
            var roles = await _context.ApplicationRoles
                .Where(r => request.RoleIds.Contains(r.Id))
                .Select(r => r.Name!)
                .ToListAsync(cancellationToken);
            roleNamesToAssign.AddRange(roles);
        }

        if (request.RoleNames is { Count: > 0 })
        {
            roleNamesToAssign.AddRange(request.RoleNames);
        }

        // SECURITY: holding users.write lets you administer your own agency's people. It does not
        // let you hand out a role that reaches past the agency — otherwise any agency owner could
        // name themselves SuperAdmin here and then read every other agency on the platform.
        var forbidden = UserAccessGuard.RolesCallerMayNotGrant(_currentUser, roleNamesToAssign);
        if (forbidden.Count > 0)
            return Result<bool>.Failure(
                $"You cannot assign {string.Join(" or ", forbidden)} — that is a platform role.", 403);

        // Get current roles
        var currentRoles = await _userManager.GetRolesAsync(user);

        // The same rule applies in reverse: this handler replaces the user's whole role set, so
        // without the check below a tenant admin could strip a platform role rather than grant one.
        var protectedExisting = UserAccessGuard.RolesCallerMayNotGrant(_currentUser, currentRoles);
        if (protectedExisting.Count > 0)
            return Result<bool>.Failure(
                $"You cannot change {string.Join(" or ", protectedExisting)} on this account.", 403);

        // Remove all current roles
        if (currentRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
                return Result<bool>.Failure(string.Join("; ", removeResult.Errors.Select(e => e.Description)), 400);
        }

        // Add new roles (only valid ones)
        if (roleNamesToAssign.Count > 0)
        {
            var validRoles = new List<string>();
            foreach (var roleName in roleNamesToAssign.Distinct())
            {
                if (await _roleManager.RoleExistsAsync(roleName))
                    validRoles.Add(roleName);
            }

            if (validRoles.Count > 0)
            {
                var addResult = await _userManager.AddToRolesAsync(user, validRoles);
                if (!addResult.Succeeded)
                    return Result<bool>.Failure(string.Join("; ", addResult.Errors.Select(e => e.Description)), 400);
            }
        }

        return Result<bool>.Success(true);
    }
}
