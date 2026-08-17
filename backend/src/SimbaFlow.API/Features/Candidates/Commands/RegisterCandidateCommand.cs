using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Events;
using System.Text.Json;
using SimbaFlow.API.Features.Partners;

namespace SimbaFlow.API.Features.Candidates.Commands;

public record RegisterCandidateCommand(
    string FirstName,
    string LastName,
    string? MiddleName,
    string PassportNumber,
    string DateOfBirth,
    int Gender,
    string? Nationality,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? City,
    string? Country,
    string? LabourId,
    string? CountryOfTravel,
    string? OfficeName,
    Guid? PartnerAgencyId,
    string? ContractDate,
    Guid OfficeId,
    CandidateIntakePayload? Intake = null) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "candidate.create";
}

public class RegisterCandidateHandler : IRequestHandler<RegisterCandidateCommand, Result<Guid>>
{
    private readonly ITenantDbContext _context;
    private readonly IApplicationDbContext _appContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IPlatformDbContext _platform;

    public RegisterCandidateHandler(
        ITenantDbContext context,
        IApplicationDbContext appContext,
        ICurrentUserService currentUser,
        IPlatformDbContext platform)
    {
        _context = context;
        _appContext = appContext;
        _currentUser = currentUser;
        _platform = platform;
    }

    public async Task<Result<Guid>> Handle(RegisterCandidateCommand request, CancellationToken cancellationToken)
    {
        var passportExists = await _context.Candidates
            .AnyAsync(c => c.PassportNumber == request.PassportNumber && !c.IsDeleted, cancellationToken);

        if (passportExists)
            return Result<Guid>.Failure("A candidate with this passport number already exists.", 409);

        // A candidate may only be placed through a partner this agency holds a live agreement with.
        var partnerCheck = await PartnerLinkValidator.CheckAsync(
            _platform, _currentUser.TenantId, request.PartnerAgencyId, cancellationToken);
        if (!partnerCheck.IsValid)
            return Result<Guid>.Failure(partnerCheck.Error!, 400);

        if (!string.IsNullOrEmpty(request.LabourId))
        {
            var labourIdExists = await _context.Candidates
                .AnyAsync(c => c.LabourId == request.LabourId && !c.IsDeleted, cancellationToken);

            if (labourIdExists)
                return Result<Guid>.Failure("A candidate with this Labour ID already exists.", 409);
        }

        var officeId = await ResolveOfficeIdAsync(request.OfficeId, cancellationToken);
        if (officeId == Guid.Empty)
            return Result<Guid>.Failure(
                "No registering office is available. Create an office under Offices, or assign a location to this user.",
                400);

        var workflowDef = await _context.WorkflowDefinitions
            .Include(w => w.Stages)
            .FirstOrDefaultAsync(w => w.IsActive && !w.IsDeleted, cancellationToken);

        var initialStage = workflowDef?.Stages.FirstOrDefault(s => s.IsInitialStage && !s.IsDeleted);

        var candidate = new Candidate
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            PassportNumber = request.PassportNumber,
            DateOfBirth = DateOnly.Parse(request.DateOfBirth),
            Gender = (Gender)request.Gender,
            Nationality = request.Nationality,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            LabourId = request.LabourId,
            CountryOfTravel = request.CountryOfTravel,
            OfficeName = request.OfficeName,
            PartnerAgencyId = request.PartnerAgencyId,
            ContractDate = string.IsNullOrEmpty(request.ContractDate) ? null : DateOnly.Parse(request.ContractDate),
            OfficeId = officeId,
            Status = CandidateStatus.Active,
            CurrentStageId = initialStage?.Id,
            CurrentStageName = initialStage?.Name,
            StageEnteredAt = DateTime.UtcNow,
            RegisteredAt = DateTime.UtcNow,
            RegisteredBy = _currentUser.UserName
        };

        CandidateIntakeMapper.Apply(candidate, request.Intake ?? new CandidateIntakePayload(), setVisaDefault: true);

        if (string.IsNullOrWhiteSpace(candidate.ApplicationNo))
            candidate.ApplicationNo = await GenerateApplicationNoAsync(cancellationToken);

        candidate.AddDomainEvent(new CandidateRegisteredEvent(
            candidate.Id, candidate.FullName, candidate.OfficeId, initialStage?.Id ?? Guid.Empty));

        _context.Candidates.Add(candidate);

        if (Guid.TryParse(_currentUser.UserId, out var userId))
        {
            _context.WorkflowEvents.Add(new WorkflowEvent
            {
                CandidateId = candidate.Id,
                SequenceNumber = 1,
                EventType = WorkflowEventType.Registered,
                ToStageId = initialStage?.Id,
                ToStageName = initialStage?.Name,
                Data = JsonDocument.Parse("{}"),
                UserId = userId,
                UserName = _currentUser.UserName ?? "unknown"
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(candidate.Id, 201);
    }

    private async Task<Guid> ResolveOfficeIdAsync(Guid requestedOfficeId, CancellationToken cancellationToken)
    {
        if (requestedOfficeId != Guid.Empty)
            return requestedOfficeId;

        if (_currentUser.ActiveLocationId is Guid locationId && locationId != Guid.Empty)
            return locationId;

        if (_currentUser.DepartmentId is Guid departmentId && departmentId != Guid.Empty)
            return departmentId;

        if (Guid.TryParse(_currentUser.UserId, out var userId))
        {
            var user = await _appContext.ApplicationUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user?.OfficeId is Guid officeId && officeId != Guid.Empty)
                return officeId;
            if (user?.ActiveLocationId is Guid activeLocation && activeLocation != Guid.Empty)
                return activeLocation;
            if (user?.DepartmentId is Guid userDept && userDept != Guid.Empty)
                return userDept;
        }

        // Prefer tenant-scoped offices, then any active department, then any location.
        var tenantId = _currentUser.TenantId;
        if (tenantId.HasValue)
        {
            var tenantOffice = await _appContext.Departments
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted && d.TenantId == tenantId)
                .OrderBy(d => d.Name)
                .Select(d => d.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (tenantOffice != Guid.Empty)
                return tenantOffice;
        }

        var anyDepartment = await _appContext.Departments
            .AsNoTracking()
            .Where(d => d.IsActive && !d.IsDeleted)
            .OrderBy(d => d.Name)
            .Select(d => d.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (anyDepartment != Guid.Empty)
            return anyDepartment;

        var anyLocation = await _appContext.Locations
            .AsNoTracking()
            .Where(l => l.IsActive && !l.IsDeleted)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Name)
            .Select(l => l.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return anyLocation;
    }

    private async Task<string> GenerateApplicationNoAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow;
        var prefix = $"APP-{today:yyyyMMdd}-";
        var countToday = await _context.Candidates
            .CountAsync(c => c.ApplicationNo != null && c.ApplicationNo.StartsWith(prefix), cancellationToken);
        return $"{prefix}{(countToday + 1):D4}";
    }
}
