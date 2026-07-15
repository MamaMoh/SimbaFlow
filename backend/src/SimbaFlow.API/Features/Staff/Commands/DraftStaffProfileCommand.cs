using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Staff;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Staff.Commands;

/// <summary>
/// HR initiates this command to create a clinical/administrative staff profile.
/// Does NOT create an IT account — that's a separate workflow (ProvisionUserAccountCommand).
/// </summary>
public record DraftStaffProfileCommand(
    string FirstName,
    string LastName,
    string? MiddleName,
    string? PreferredName,
    string? Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    StaffType StaffType,
    string? ClinicalTitle,
    string? Title,
    string? Specialization,
    EmploymentStatus EmploymentStatus,
    DateOnly? HireDate,
    bool RequiresCoSignature,
    Guid? PrimaryLocationId,
    // Initial identifiers (optional, can be added later)
    List<StaffIdentifierDto>? Identifiers) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "staff.write";
}

public record StaffIdentifierDto(
    IdentifierType IdentifierType,
    string Value,
    string Issuer,
    DateOnly IssueDate,
    DateOnly? ExpirationDate,
    bool IsPrimary);

// --- Validator ---
public class DraftStaffProfileValidator : AbstractValidator<DraftStaffProfileCommand>
{
    public DraftStaffProfileValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MiddleName).MaximumLength(100);
        RuleFor(x => x.PreferredName).MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.PhoneNumber).MaximumLength(20);
        RuleFor(x => x.ClinicalTitle).MaximumLength(50);
        RuleFor(x => x.Title).MaximumLength(100);
        RuleFor(x => x.Specialization).MaximumLength(200);
        RuleFor(x => x.StaffType).IsInEnum();
        RuleFor(x => x.EmploymentStatus).IsInEnum();

        RuleForEach(x => x.Identifiers).ChildRules(id =>
        {
            id.RuleFor(i => i.Value).NotEmpty().MaximumLength(200);
            id.RuleFor(i => i.Issuer).NotEmpty().MaximumLength(300);
            id.RuleFor(i => i.IdentifierType).IsInEnum();
        });
    }
}

// --- Handler ---
public class DraftStaffProfileHandler : IRequestHandler<DraftStaffProfileCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public DraftStaffProfileHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<Guid>> Handle(DraftStaffProfileCommand request, CancellationToken cancellationToken)
    {
        // Generate unique staff ID
        var staffId = await GenerateStaffIdAsync(cancellationToken);

        // Validate primary location if provided
        if (request.PrimaryLocationId.HasValue)
        {
            var locationExists = await _context.Locations
                .AnyAsync(l => l.Id == request.PrimaryLocationId.Value, cancellationToken);
            if (!locationExists)
                return Result<Guid>.Failure("Primary location not found", 400);
        }

        var staffProfile = new StaffProfile
        {
            StaffId = staffId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            PreferredName = request.PreferredName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            StaffType = request.StaffType,
            ClinicalTitle = request.ClinicalTitle,
            Title = request.Title,
            Specialization = request.Specialization,
            EmploymentStatus = request.EmploymentStatus,
            HireDate = request.HireDate,
            RequiresCoSignature = request.RequiresCoSignature,
            PrimaryLocationId = request.PrimaryLocationId,
            IsActive = true,
        };

        // Add identifiers if provided
        if (request.Identifiers?.Count > 0)
        {
            foreach (var dto in request.Identifiers)
            {
                staffProfile.Identifiers.Add(new StaffIdentifier
                {
                    StaffProfileId = staffProfile.Id,
                    IdentifierType = dto.IdentifierType,
                    Value = dto.Value,
                    Issuer = dto.Issuer,
                    IssueDate = dto.IssueDate,
                    ExpirationDate = dto.ExpirationDate,
                    IsPrimary = dto.IsPrimary,
                    VerificationStatus = VerificationStatus.Pending,
                });
            }
        }

        // Raise domain event
        staffProfile.AddDomainEvent(new StaffProfileDraftedEvent(
            staffProfile.Id, staffProfile.StaffType, staffProfile.StaffId));

        _context.StaffProfiles.Add(staffProfile);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(staffProfile.Id, 201);
    }

    private async Task<string> GenerateStaffIdAsync(CancellationToken ct)
    {
        // Pattern: STF-XXXXX (zero-padded sequential)
        var lastStaff = await _context.StaffProfiles
            .IgnoreQueryFilters()
            .OrderByDescending(s => s.StaffId)
            .Select(s => s.StaffId)
            .FirstOrDefaultAsync(ct);

        if (lastStaff is null || !int.TryParse(lastStaff.Replace("STF-", ""), out var lastNumber))
            return "STF-00001";

        return $"STF-{(lastNumber + 1):D5}";
    }
}
