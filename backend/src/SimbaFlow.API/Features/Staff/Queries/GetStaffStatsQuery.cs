using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Staff.Queries;

public record StaffStatsDto(
    int TotalStaff,
    int ActiveProviders,
    int PendingProvisioning,
    int ExpiringCredentials);

public record GetStaffStatsQuery : IRequest<Result<StaffStatsDto>>, IRequirePermission
{
    public string RequiredPermission => "staff.read";
}

public class GetStaffStatsHandler : IRequestHandler<GetStaffStatsQuery, Result<StaffStatsDto>>
{
    private readonly IPlatformDbContext _context;

    public GetStaffStatsHandler(IPlatformDbContext context) => _context = context;

    public async Task<Result<StaffStatsDto>> Handle(GetStaffStatsQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thirtyDaysFromNow = today.AddDays(30);

        var totalStaff = await _context.StaffProfiles.CountAsync(ct);

        var activeProviders = await _context.StaffProfiles
            .CountAsync(s => s.IsActive && (
                s.EmploymentStatus == EmploymentStatus.Active
                || s.EmploymentStatus == EmploymentStatus.Active
                || s.EmploymentStatus == EmploymentStatus.Active), ct);

        var pendingProvisioning = await _context.StaffProfiles
            .CountAsync(s => s.IsActive && s.UserId == null, ct);

        var expiringCredentials = await _context.StaffIdentifiers
            .CountAsync(i => !i.IsDeleted
                && i.ExpirationDate != null
                && i.ExpirationDate >= today
                && i.ExpirationDate <= thirtyDaysFromNow, ct);

        return Result<StaffStatsDto>.Success(new StaffStatsDto(
            totalStaff, activeProviders, pendingProvisioning, expiringCredentials));
    }
}
