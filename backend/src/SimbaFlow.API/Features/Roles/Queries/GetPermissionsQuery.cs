using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Roles.Queries;

public record GetPermissionsQuery : IRequest<Result<List<PermissionGroupDto>>>, IRequirePermission
{
    public string RequiredPermission => "role.read";
}

public record PermissionGroupDto(string Module, IReadOnlyList<PermissionDto> Permissions);
public record PermissionDto(Guid Id, string Code, string Name, string? Description);

public class GetPermissionsHandler : IRequestHandler<GetPermissionsQuery, Result<List<PermissionGroupDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetPermissionsHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<List<PermissionGroupDto>>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        var permissions = await _context.Permissions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.IsActive)
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var grouped = permissions
            .GroupBy(p => p.Module)
            .Select(g => new PermissionGroupDto(
                g.Key,
                g.Select(p => new PermissionDto(p.Id, p.Code, p.Name, p.Description)).ToList()))
            .ToList();

        return Result<List<PermissionGroupDto>>.Success(grouped);
    }
}
