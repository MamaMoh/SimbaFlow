using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Roles.Commands;

public record DeleteRoleCommand(Guid Id) : IRequest<Result<bool>>, IRequirePermission
{
    public string RequiredPermission => "role.write";
}

public class DeleteRoleHandler : IRequestHandler<DeleteRoleCommand, Result<bool>>
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;

    public DeleteRoleHandler(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext context)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _context = context;
    }

    public async Task<Result<bool>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByIdAsync(request.Id.ToString());
        if (role is null)
            return Result<bool>.Failure("Role not found", 404);

        if (role.IsSystemRole)
            return Result<bool>.Failure("Cannot delete a system role", 403);

        // Check if role is assigned to any users
        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
            return Result<bool>.Failure("Cannot delete a role that is assigned to users. Remove the role from all users first.", 409);

        // Remove role permissions first
        var rolePermissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync(cancellationToken);

        _context.RolePermissions.RemoveRange(rolePermissions);
        await _context.SaveChangesAsync(cancellationToken);

        // Delete the role
        var deleteResult = await _roleManager.DeleteAsync(role);
        if (!deleteResult.Succeeded)
            return Result<bool>.Failure(string.Join("; ", deleteResult.Errors.Select(e => e.Description)), 400);

        return Result<bool>.Success(true);
    }
}
