using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Staff.Queries;

// --- DTOs ---
public record StaffProfileDetailDto(
    Guid Id,
    string StaffId,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? PreferredName,
    string? Email,
    string? PhoneNumber,
    string? DateOfBirth,
    string StaffType,
    string? ClinicalTitle,
    string? Title,
    string? Specialization,
    string EmploymentStatus,
    string? HireDate,
    string? TerminationDate,
    bool RequiresCoSignature,
    bool IsActive,
    Guid? UserId,
    string? Username,
    bool? UserIsActive,
    bool? RequireMfa,
    DateTime? LastLoginAt,
    Guid? PrimaryLocationId,
    string? PrimaryLocationName,
    List<StaffIdentifierDto> Identifiers,
    List<StaffAffiliationDto> Affiliations,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record StaffIdentifierDto(
    Guid Id,
    string IdentifierType,
    string Value,
    string Issuer,
    string IssueDate,
    string? ExpirationDate,
    bool IsPrimary,
    string VerificationStatus,
    DateTime? VerifiedAt,
    string? VerifiedBy);

public record StaffAffiliationDto(
    Guid Id,
    Guid DepartmentId,
    string DepartmentName,
    string ClinicalRole,
    string EffectiveStartDate,
    string? EffectiveEndDate,
    bool IsPrimary,
    string? Notes,
    bool IsCurrentlyActive);

// --- Query ---
public record GetStaffProfileByIdQuery(Guid Id) : IRequest<Result<StaffProfileDetailDto>>, IRequirePermission
{
    public string RequiredPermission => "staff.read";
}

// --- Handler ---
public class GetStaffProfileByIdHandler : IRequestHandler<GetStaffProfileByIdQuery, Result<StaffProfileDetailDto>>
{
    private readonly IPlatformDbContext _context;

    public GetStaffProfileByIdHandler(IPlatformDbContext context) => _context = context;

    public async Task<Result<StaffProfileDetailDto>> Handle(GetStaffProfileByIdQuery request, CancellationToken ct)
    {
        var profile = await _context.StaffProfiles
            .AsNoTracking()
            .Include(s => s.Identifiers.Where(i => !i.IsDeleted))
            .Include(s => s.DepartmentAffiliations.Where(a => !a.IsDeleted))
            .Include(s => s.PrimaryLocation)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);

        if (profile is null)
            return Result<StaffProfileDetailDto>.Failure("Staff profile not found", 404);

        // Fetch linked user info if exists
        string? username = null;
        bool? userIsActive = null;
        bool? requireMfa = null;
        DateTime? lastLoginAt = null;

        if (profile.UserId.HasValue)
        {
            var user = await _context.ApplicationUsers
                .AsNoTracking()
                .Where(u => u.Id == profile.UserId.Value)
                .Select(u => new { u.UserName, u.IsActive, u.RequireMfa, u.LastLoginAt })
                .FirstOrDefaultAsync(ct);

            if (user is not null)
            {
                username = user.UserName;
                userIsActive = user.IsActive;
                requireMfa = user.RequireMfa;
                lastLoginAt = user.LastLoginAt;
            }
        }

        // Fetch department names for affiliations
        var deptIds = profile.DepartmentAffiliations.Select(a => a.DepartmentId).Distinct().ToList();
        var deptNames = await _context.Departments
            .AsNoTracking()
            .Where(d => deptIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var dto = new StaffProfileDetailDto(
            profile.Id,
            profile.StaffId,
            profile.FirstName,
            profile.LastName,
            profile.MiddleName,
            profile.PreferredName,
            profile.Email,
            profile.PhoneNumber,
            profile.DateOfBirth?.ToString("yyyy-MM-dd"),
            profile.StaffType.ToString(),
            profile.ClinicalTitle,
            profile.Title,
            profile.Specialization,
            profile.EmploymentStatus.ToString(),
            profile.HireDate?.ToString("yyyy-MM-dd"),
            profile.TerminationDate?.ToString("yyyy-MM-dd"),
            profile.RequiresCoSignature,
            profile.IsActive,
            profile.UserId,
            username,
            userIsActive,
            requireMfa,
            lastLoginAt,
            profile.PrimaryLocationId,
            profile.PrimaryLocation?.Name,
            profile.Identifiers.Select(i => new StaffIdentifierDto(
                i.Id,
                i.IdentifierType.ToString(),
                i.Value,
                i.Issuer,
                i.IssueDate.ToString("yyyy-MM-dd"),
                i.ExpirationDate?.ToString("yyyy-MM-dd"),
                i.IsPrimary,
                i.VerificationStatus.ToString(),
                i.VerifiedAt,
                i.VerifiedBy)).ToList(),
            profile.DepartmentAffiliations.Select(a => new StaffAffiliationDto(
                a.Id,
                a.DepartmentId,
                deptNames.GetValueOrDefault(a.DepartmentId, "Unknown"),
                a.ClinicalRole,
                a.EffectiveStartDate.ToString("yyyy-MM-dd"),
                a.EffectiveEndDate?.ToString("yyyy-MM-dd"),
                a.IsPrimary,
                a.Notes,
                a.EffectiveStartDate <= today && (a.EffectiveEndDate == null || a.EffectiveEndDate >= today)
            )).ToList(),
            profile.CreatedAt,
            profile.UpdatedAt);

        return Result<StaffProfileDetailDto>.Success(dto);
    }
}
