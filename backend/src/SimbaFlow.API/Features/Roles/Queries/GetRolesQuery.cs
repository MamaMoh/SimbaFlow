using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Roles.Queries;

public record GetRolesQuery : IRequest<Result<List<RoleListDto>>>, IRequirePermission
{
    public string RequiredPermission => "role.read";
}

public record RoleListDto(Guid Id, string Name, string? Description, bool IsSystemRole, bool IsActive, int PermissionCount);

public class GetRolesHandler : IRequestHandler<GetRolesQuery, Result<List<RoleListDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetRolesHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<List<RoleListDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _context.ApplicationRoles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
            .OrderBy(r => r.Name)
            .Select(r => new RoleListDto(
                r.Id, r.Name!, r.Description, r.IsSystemRole, r.IsActive, r.RolePermissions.Count))
            .ToListAsync(cancellationToken);

        return Result<List<RoleListDto>>.Success(roles);
    }
}
