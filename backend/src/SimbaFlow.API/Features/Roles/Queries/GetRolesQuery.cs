using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Roles.Queries;

public record GetRolesQuery : IRequest<Result<List<RoleListDto>>>, IRequirePermission
{
    public string RequiredPermission => "role.read";
}

public record RoleListDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    bool IsActive,
    int PermissionCount,
    IReadOnlyList<string> Permissions,
    int UserCount);

public class GetRolesHandler : IRequestHandler<GetRolesQuery, Result<List<RoleListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetRolesHandler(IApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<Result<List<RoleListDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        // The permission codes and the headcount are both shown on the roles screen. They are
        // gathered here rather than in the page so that what the screen displays is read from the
        // same tables that actually decide access — the point of the screen is to tell the truth
        // about who can do what.
        var roles = await _context.ApplicationRoles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                r.IsSystemRole,
                r.IsActive,
                Permissions = r.RolePermissions
                    .Select(rp => rp.Permission.Code)
                    .OrderBy(c => c)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        // One query per role, which is fine for the handful of roles an agency has, and keeps the
        // membership read going through UserManager as it does everywhere else.
        var result = new List<RoleListDto>(roles.Count);
        foreach (var r in roles)
        {
            var members = await _userManager.GetUsersInRoleAsync(r.Name!);
            result.Add(new RoleListDto(
                r.Id, r.Name!, r.Description, r.IsSystemRole, r.IsActive,
                r.Permissions.Count, r.Permissions,
                members.Count(u => !u.IsDeleted)));
        }

        return Result<List<RoleListDto>>.Success(result);
    }
}
