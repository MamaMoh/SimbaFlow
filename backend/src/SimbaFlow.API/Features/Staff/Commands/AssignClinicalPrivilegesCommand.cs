using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Staff;

namespace SimbaFlow.API.Features.Staff.Commands;

/// <summary>
/// Medical Administration assigns a staff member to a department with a specific clinical role
/// and effective dates. This is separate from IT account provisioning — a provider can have
/// clinical privileges without having logged in yet.
/// </summary>
public record AssignClinicalPrivilegesCommand(
    Guid StaffProfileId,
    Guid DepartmentId,
    string ClinicalRole,
    DateOnly EffectiveStartDate,
    DateOnly? EffectiveEndDate,
    bool IsPrimary,
    string? Notes) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "staff.privileges.write";
}

// --- Validator ---
public class AssignClinicalPrivilegesValidator : AbstractValidator<AssignClinicalPrivilegesCommand>
{
    public AssignClinicalPrivilegesValidator()
    {
        RuleFor(x => x.StaffProfileId).NotEmpty();
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.ClinicalRole).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EffectiveStartDate).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.EffectiveEndDate == null || x.EffectiveEndDate >= x.EffectiveStartDate)
            .WithMessage("End date must be on or after start date");
    }
}

// --- Handler ---
public class AssignClinicalPrivilegesHandler : IRequestHandler<AssignClinicalPrivilegesCommand, Result<Guid>>
{
    private readonly IPlatformDbContext _context;

    public AssignClinicalPrivilegesHandler(IPlatformDbContext context) => _context = context;

    public async Task<Result<Guid>> Handle(AssignClinicalPrivilegesCommand request, CancellationToken cancellationToken)
    {
        // Verify staff profile exists
        var staffProfile = await _context.StaffProfiles
            .Include(s => s.DepartmentAffiliations)
            .FirstOrDefaultAsync(s => s.Id == request.StaffProfileId, cancellationToken);

        if (staffProfile is null)
            return Result<Guid>.Failure("Staff profile not found", 404);

        // Verify department exists
        var departmentExists = await _context.Departments
            .AnyAsync(d => d.Id == request.DepartmentId, cancellationToken);
        if (!departmentExists)
            return Result<Guid>.Failure("Department not found", 404);

        // Check for overlapping affiliation in the same department
        var hasOverlap = staffProfile.DepartmentAffiliations.Any(a =>
            a.DepartmentId == request.DepartmentId
            && !a.IsDeleted
            && a.EffectiveStartDate <= (request.EffectiveEndDate ?? DateOnly.MaxValue)
            && (a.EffectiveEndDate ?? DateOnly.MaxValue) >= request.EffectiveStartDate);

        if (hasOverlap)
            return Result<Guid>.Failure(
                "Staff member already has an overlapping affiliation in this department", 409);

        // If marking as primary, clear existing primary for this staff member
        if (request.IsPrimary)
        {
            foreach (var existing in staffProfile.DepartmentAffiliations.Where(a => a.IsPrimary && !a.IsDeleted))
            {
                existing.IsPrimary = false;
            }
        }

        var affiliation = new StaffDepartmentAffiliation
        {
            StaffProfileId = request.StaffProfileId,
            DepartmentId = request.DepartmentId,
            ClinicalRole = request.ClinicalRole,
            EffectiveStartDate = request.EffectiveStartDate,
            EffectiveEndDate = request.EffectiveEndDate,
            IsPrimary = request.IsPrimary,
            Notes = request.Notes,
        };

        _context.StaffDepartmentAffiliations.Add(affiliation);

        // Raise domain event
        staffProfile.AddDomainEvent(new ClinicalPrivilegesAssignedEvent(
            request.StaffProfileId, request.DepartmentId,
            request.ClinicalRole, request.EffectiveStartDate));

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(affiliation.Id, 201);
    }
}
