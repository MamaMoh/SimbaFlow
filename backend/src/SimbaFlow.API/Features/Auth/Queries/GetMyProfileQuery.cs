using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Auth.Queries;

// --- Query ---
public record GetMyProfileQuery : IRequest<Result<MyProfileDto>>;

// --- Response ---
public record MyProfileDto(
    Guid Id,
    string Username,
    string FullName,
    string FirstName,
    string LastName,
    string? MiddleName,
    string Email,
    string? PhoneNumber,
    string? ProfileImageUrl,
    bool IsSuperAdmin,
    bool TwoFactorEnabled,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? ActiveLocationId,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Roles);

// --- Handler ---
public class GetMyProfileHandler : IRequestHandler<GetMyProfileQuery, Result<MyProfileDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyProfileHandler(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<MyProfileDto>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
            return Result<MyProfileDto>.Failure("Not authenticated", 401);

        var user = await _userManager.FindByIdAsync(_currentUser.UserId);
        if (user is null)
            return Result<MyProfileDto>.Failure("User not found", 404);

        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var permissions = await _context.RolePermissions
            .Include(rp => rp.Permission)
            .Include(rp => rp.Role)
            .Where(rp => roles.Contains(rp.Role.Name!))
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (user.IsSuperAdmin)
            permissions = ["system.admin", .. permissions];

        string? departmentName = null;
        if (user.DepartmentId.HasValue)
        {
            departmentName = await _context.Departments
                .Where(d => d.Id == user.DepartmentId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Result<MyProfileDto>.Success(new MyProfileDto(
            user.Id, user.UserName!, user.FullName, user.FirstName, user.LastName,
            user.MiddleName, user.Email!, user.PhoneNumber, user.ProfileImageUrl,
            user.IsSuperAdmin, user.TwoFactorEnabled, user.DepartmentId, departmentName,
            user.ActiveLocationId, permissions, roles));
    }
}
