using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Users.Queries;

public record GetUserRolesQuery(Guid UserId) : IRequest<Result<List<UserRoleItemDto>>>, IRequirePermission
{
    public string RequiredPermission => "users.read";
}

public record UserRoleItemDto(Guid Id, string Name, string? Description);

public class GetUserRolesHandler : IRequestHandler<GetUserRolesQuery, Result<List<UserRoleItemDto>>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPlatformDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUserRolesHandler(UserManager<ApplicationUser> userManager, IPlatformDbContext context, ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<UserRoleItemDto>>> Handle(GetUserRolesQuery request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<List<UserRoleItemDto>>.Failure("User not found", 404);

        // SECURITY: tenant admins may only view roles of users in their own tenant.
        if (!UserAccessGuard.CanManage(_currentUser, user))
            return Result<List<UserRoleItemDto>>.Failure("User not found", 404);

        var roleNames = await _userManager.GetRolesAsync(user);
        var roles = new List<UserRoleItemDto>();

        foreach (var roleName in roleNames)
        {
            var role = await _context.ApplicationRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
            if (role is not null)
                roles.Add(new UserRoleItemDto(role.Id, role.Name!, role.Description));
        }

        return Result<List<UserRoleItemDto>>.Success(roles);
    }
}
