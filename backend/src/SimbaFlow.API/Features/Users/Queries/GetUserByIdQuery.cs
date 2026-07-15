using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Users.Queries;

public record GetUserByIdQuery(Guid Id) : IRequest<Result<UserDetailDto>>, IRequirePermission
{
    public string RequiredPermission => "users.read";
}

public record UserDetailDto(
    Guid Id,
    string Username,
    string FirstName,
    string LastName,
    string? MiddleName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool IsSuperAdmin,
    Guid? DepartmentId,
    string? DepartmentName,
    DateTime? LastLoginAt,
    IReadOnlyList<UserRoleDto> Roles,
    DateTime CreatedAt);

public record UserRoleDto(Guid Id, string Name);

public class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, Result<UserDetailDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;

    public GetUserByIdHandler(UserManager<ApplicationUser> userManager, IApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<Result<UserDetailDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.Id.ToString());
        if (user is null)
            return Result<UserDetailDto>.Failure("User not found", 404);

        // Load department
        string? departmentName = null;
        if (user.DepartmentId.HasValue)
        {
            var dept = await _context.Departments.FindAsync(new object[] { user.DepartmentId.Value }, cancellationToken);
            departmentName = dept?.Name;
        }

        // Get roles
        var roleNames = await _userManager.GetRolesAsync(user);
        var roles = new List<UserRoleDto>();
        foreach (var roleName in roleNames)
        {
            var role = await _context.ApplicationRoles
                .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
            if (role is not null)
                roles.Add(new UserRoleDto(role.Id, role.Name!));
        }

        var dto = new UserDetailDto(
            user.Id,
            user.UserName!,
            user.FirstName,
            user.LastName,
            user.MiddleName,
            user.Email!,
            user.PhoneNumber,
            user.IsActive,
            user.IsSuperAdmin,
            user.DepartmentId,
            departmentName,
            user.LastLoginAt,
            roles,
            user.CreatedAt);

        return Result<UserDetailDto>.Success(dto);
    }
}
