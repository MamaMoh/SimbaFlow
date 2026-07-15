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
    private readonly IApplicationDbContext _context;

    public AssignRolesHandler(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<Result<bool>> Handle(AssignRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
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

        // Get current roles
        var currentRoles = await _userManager.GetRolesAsync(user);

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
