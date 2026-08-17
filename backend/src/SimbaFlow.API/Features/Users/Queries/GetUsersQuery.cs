using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Users.Queries;

public record GetUsersQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IRequest<Result<PaginatedList<UserListDto>>>, IRequirePermission
{
    public string RequiredPermission => "users.read";
}

public record UserListDto(
    Guid Id,
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool IsSuperAdmin,
    bool TwoFactorEnabled,
    string? DepartmentName,
    Guid? TenantId,
    string? TenantName,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt);

public class GetUsersHandler : IRequestHandler<GetUsersQuery, Result<PaginatedList<UserListDto>>>
{
    private readonly IPlatformDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUserService;

    public GetUsersHandler(IPlatformDbContext context, UserManager<ApplicationUser> userManager, ICurrentUserService currentUserService)
    {
        _context = context;
        _userManager = userManager;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginatedList<UserListDto>>> Handle(
        GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ApplicationUsers
            .AsNoTracking()
            .Include(u => u.Department)
            .AsQueryable();

        // Tenant isolation: non-SuperAdmin users can only see users from their own tenant
        if (!_currentUserService.IsSuperAdmin && _currentUserService.TenantId.HasValue)
        {
            query = query.Where(u => u.TenantId == _currentUserService.TenantId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(u =>
                u.UserName!.ToLower().Contains(search) ||
                u.FirstName.ToLower().Contains(search) ||
                u.LastName.ToLower().Contains(search) ||
                u.Email!.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UserListDto>();
        var tenantIds = users.Where(u => u.TenantId.HasValue).Select(u => u.TenantId!.Value).Distinct().ToList();
        var tenants = tenantIds.Count > 0
            ? await _context.Tenants.AsNoTracking()
                .Where(t => tenantIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken)
            : new Dictionary<Guid, string>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var tenantName = user.TenantId.HasValue && tenants.TryGetValue(user.TenantId.Value, out var name) ? name : null;
            items.Add(new UserListDto(
                user.Id, user.UserName!, user.FirstName, user.LastName,
                user.Email!, user.PhoneNumber, user.IsActive, user.IsSuperAdmin,
                user.TwoFactorEnabled, user.Department?.Name,
                user.TenantId, tenantName,
                user.LastLoginAt, roles.ToList(), user.CreatedAt));
        }

        var result = new PaginatedList<UserListDto>(items, totalCount, request.Page, request.PageSize);
        return Result<PaginatedList<UserListDto>>.Success(result);
    }
}
