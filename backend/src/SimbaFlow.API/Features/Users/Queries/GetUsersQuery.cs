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
    bool TenantRemoved,
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

    private bool IsPlatformAdmin() =>
        !_currentUserService.TenantId.HasValue
        && _currentUserService.Roles.Contains("PlatformAdmin", StringComparer.OrdinalIgnoreCase);

    public async Task<Result<PaginatedList<UserListDto>>> Handle(
        GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ApplicationUsers
            .AsNoTracking()
            .Include(u => u.Department)
            .AsQueryable();

        // Tenant isolation, stated so that every case is covered.
        //
        // The previous form — "not a super admin AND has a tenant" — fell through with no filter at
        // all for a caller who was neither, and returned every user on the platform with their email
        // address. That is not a hypothetical shape: PlatformAdmin is seeded with no tenant by
        // design. Absence of a tenant must never widen a query.
        if (_currentUserService.IsSuperAdmin || IsPlatformAdmin())
        {
            // Administers accounts across the platform; sees them all.
        }
        else if (_currentUserService.TenantId is Guid callerTenant)
        {
            query = query.Where(u => u.TenantId == callerTenant);
        }
        else
        {
            // Neither platform nor tenant: entitled to nobody.
            query = query.Where(u => false);
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
        // Deleted agencies are included deliberately. Their people still exist and still carry the
        // agency id, so filtering them out here would blank the column and make the account look
        // like a platform one. What the screen needs is the name *and* the fact that it is gone —
        // otherwise this list and the agencies list disagree with no explanation on either.
        var tenants = tenantIds.Count > 0
            ? await _context.Tenants.AsNoTracking()
                .Where(t => tenantIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name, t.IsDeleted })
                .ToDictionaryAsync(t => t.Id, t => t, cancellationToken)
            : [];

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var tenant = user.TenantId.HasValue && tenants.TryGetValue(user.TenantId.Value, out var t) ? t : null;
            // No row at all is as removed as a soft-deleted one, from where this user stands.
            var tenantRemoved = user.TenantId.HasValue && (tenant is null || tenant.IsDeleted);
            items.Add(new UserListDto(
                user.Id, user.UserName!, user.FirstName, user.LastName,
                user.Email!, user.PhoneNumber, user.IsActive, user.IsSuperAdmin,
                user.TwoFactorEnabled, user.Department?.Name,
                user.TenantId, tenant?.Name, tenantRemoved,
                user.LastLoginAt, roles.ToList(), user.CreatedAt));
        }

        var result = new PaginatedList<UserListDto>(items, totalCount, request.Page, request.PageSize);
        return Result<PaginatedList<UserListDto>>.Success(result);
    }
}
