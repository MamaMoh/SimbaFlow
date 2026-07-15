using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Roles.Queries;

public record GetRoleByIdQuery(Guid Id) : IRequest<Result<RoleDetailDto>>, IRequirePermission
{
    public string RequiredPermission => "role.read";
}

public record RoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    List<Guid> PermissionIds);

public class GetRoleByIdHandler : IRequestHandler<GetRoleByIdQuery, Result<RoleDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRoleByIdHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<RoleDetailDto>> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await _context.ApplicationRoles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role is null)
            return Result<RoleDetailDto>.Failure("Role not found", 404);

        var dto = new RoleDetailDto(
            role.Id,
            role.Name!,
            role.Description,
            role.IsSystemRole,
            role.RolePermissions.Select(rp => rp.PermissionId).ToList());

        return Result<RoleDetailDto>.Success(dto);
    }
}
