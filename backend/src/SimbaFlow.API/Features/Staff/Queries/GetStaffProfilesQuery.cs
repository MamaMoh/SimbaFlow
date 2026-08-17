using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Staff.Queries;

// --- List DTO ---
public record StaffProfileListDto(
    Guid Id,
    string StaffId,
    string FirstName,
    string LastName,
    string? PreferredName,
    string? Email,
    string StaffType,
    string? ClinicalTitle,
    string? Specialization,
    string EmploymentStatus,
    string? PrimaryDepartmentName,
    bool RequiresCoSignature,
    bool IsActive,
    Guid? UserId,
    string? HireDate,
    DateTime CreatedAt);

// --- Query ---
public record GetStaffProfilesQuery(
    string? Search,
    string? EmploymentStatusFilter,
    string? StaffTypeFilter,
    Guid? DepartmentFilter,
    bool? HasAccountFilter) : IRequest<Result<List<StaffProfileListDto>>>, IRequirePermission
{
    public string RequiredPermission => "staff.read";
}

// --- Handler ---
public class GetStaffProfilesHandler : IRequestHandler<GetStaffProfilesQuery, Result<List<StaffProfileListDto>>>
{
    private readonly IPlatformDbContext _context;

    public GetStaffProfilesHandler(IPlatformDbContext context) => _context = context;

    public async Task<Result<List<StaffProfileListDto>>> Handle(GetStaffProfilesQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.StaffProfiles.AsNoTracking().AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(s =>
                s.FirstName.ToLower().Contains(search) ||
                s.LastName.ToLower().Contains(search) ||
                s.StaffId.ToLower().Contains(search) ||
                (s.Email != null && s.Email.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.EmploymentStatusFilter) &&
            Enum.TryParse<EmploymentStatus>(request.EmploymentStatusFilter, out var status))
        {
            query = query.Where(s => s.EmploymentStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(request.StaffTypeFilter) &&
            Enum.TryParse<StaffType>(request.StaffTypeFilter, out var staffType))
        {
            query = query.Where(s => s.StaffType == staffType);
        }

        if (request.HasAccountFilter.HasValue)
        {
            query = request.HasAccountFilter.Value
                ? query.Where(s => s.UserId != null)
                : query.Where(s => s.UserId == null);
        }

        if (request.DepartmentFilter.HasValue)
        {
            var deptId = request.DepartmentFilter.Value;
            var staffIdsInDept = _context.StaffDepartmentAffiliations
                .Where(a => a.DepartmentId == deptId && !a.IsDeleted
                    && a.EffectiveStartDate <= today
                    && (a.EffectiveEndDate == null || a.EffectiveEndDate >= today))
                .Select(a => a.StaffProfileId);

            query = query.Where(s => staffIdsInDept.Contains(s.Id));
        }

        var profiles = await query
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Take(500)
            .ToListAsync(ct);

        // Resolve primary department names
        var profileIds = profiles.Select(p => p.Id).ToList();
        var primaryAffiliations = await _context.StaffDepartmentAffiliations
            .AsNoTracking()
            .Where(a => profileIds.Contains(a.StaffProfileId)
                && a.IsPrimary && !a.IsDeleted
                && a.EffectiveStartDate <= today
                && (a.EffectiveEndDate == null || a.EffectiveEndDate >= today))
            .Join(_context.Departments, a => a.DepartmentId, d => d.Id,
                (a, d) => new { a.StaffProfileId, DepartmentName = d.Name })
            .ToDictionaryAsync(x => x.StaffProfileId, x => x.DepartmentName, ct);

        var result = profiles.Select(s => new StaffProfileListDto(
            s.Id,
            s.StaffId,
            s.FirstName,
            s.LastName,
            s.PreferredName,
            s.Email,
            s.StaffType.ToString(),
            s.ClinicalTitle,
            s.Specialization,
            s.EmploymentStatus.ToString(),
            primaryAffiliations.GetValueOrDefault(s.Id),
            s.RequiresCoSignature,
            s.IsActive,
            s.UserId,
            s.HireDate?.ToString("yyyy-MM-dd"),
            s.CreatedAt)).ToList();

        return Result<List<StaffProfileListDto>>.Success(result);
    }
}
