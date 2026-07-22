using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Events;

namespace SimbaFlow.API.Features.Candidates.Commands;

public record PlacementInput(
    string? CountryOfTravel,
    string? WorksIn,
    string? VisaNumber,
    string? VisaType,
    string? SponsorId,
    string? SponsorName,
    string? SponsorPhone,
    string? Agent,
    string? ContractNumber,
    string? ContractDate);

public record RelativeInput(
    string RelativeName,
    string? RelativePhone,
    string? RelativeKinship);

public record SkillsInput(
    string? EnglishLevel,
    string? ArabicLevel,
    bool? CanIron,
    bool? CanSew,
    bool? CanBabysit,
    bool? CanChildcare,
    bool? CanArabicCooking,
    bool? CanClean,
    bool? CanWash,
    bool? CanCook);

public record RegisterCandidateCommand(
    string FirstName,
    string LastName,
    string? MiddleName,
    string PassportNumber,
    string DateOfBirth,
    int Gender,
    string? Nationality,
    string? Religion,
    string? MaritalStatus,
    string? Occupation,
    string? Qualification,
    string? PhoneNumber,
    string? Phone2,
    string? Email,
    string? Address,
    string? City,
    string? Country,
    string? Region,
    string? Subcity,
    string? Woreda,
    string? HouseNo,
    string? LabourId,
    string? CountryOfTravel,
    string? OfficeName,
    string? ContractDate,
    string? PassportType,
    string? PassportIssueDate,
    string? PassportExpiryDate,
    string? PlaceOfIssue,
    string? PlaceOfBirth,
    Guid OfficeId,
    PlacementInput? Placement,
    RelativeInput? Relative,
    SkillsInput? Skills) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "candidate.create";
}

public class RegisterCandidateHandler : IRequestHandler<RegisterCandidateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowEngineService _workflowEngine;

    public RegisterCandidateHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IWorkflowEngineService workflowEngine)
    {
        _context = context;
        _currentUser = currentUser;
        _workflowEngine = workflowEngine;
    }

    public async Task<Result<Guid>> Handle(RegisterCandidateCommand request, CancellationToken cancellationToken)
    {
        var passportExists = await _context.Candidates
            .AnyAsync(c => c.PassportNumber == request.PassportNumber && !c.IsDeleted, cancellationToken);
        if (passportExists)
            return Result<Guid>.Failure("A candidate with this passport number already exists.", 409);

        if (!string.IsNullOrEmpty(request.LabourId))
        {
            var labourIdExists = await _context.Candidates
                .AnyAsync(c => c.LabourId == request.LabourId && !c.IsDeleted, cancellationToken);
            if (labourIdExists)
                return Result<Guid>.Failure("A candidate with this Labour ID already exists.", 409);
        }

        var office = await _context.Offices
            .FirstOrDefaultAsync(o => o.Id == request.OfficeId && !o.IsDeleted, cancellationToken);
        if (office is null)
            return Result<Guid>.Failure("Office not found.", 400);

        var workflowDef = await _context.WorkflowDefinitions
            .Include(w => w.Stages)
            .FirstOrDefaultAsync(w => w.IsActive && !w.IsDeleted, cancellationToken);
        var initialStage = workflowDef?.Stages.FirstOrDefault(s => s.IsInitialStage && !s.IsDeleted);

        var appNo = $"E{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}";

        var candidate = new Candidate
        {
            ApplicationNo = appNo,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            PassportNumber = request.PassportNumber,
            PassportType = request.PassportType,
            PassportIssueDate = ParseDate(request.PassportIssueDate),
            PassportExpiryDate = ParseDate(request.PassportExpiryDate),
            PlaceOfIssue = request.PlaceOfIssue,
            PlaceOfBirth = request.PlaceOfBirth,
            DateOfBirth = DateOnly.Parse(request.DateOfBirth),
            Gender = (Gender)request.Gender,
            Nationality = request.Nationality ?? "Ethiopia",
            Religion = request.Religion,
            MaritalStatus = request.MaritalStatus,
            Occupation = request.Occupation,
            Qualification = request.Qualification,
            PhoneNumber = request.PhoneNumber,
            Phone2 = request.Phone2,
            Email = request.Email,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            Region = request.Region,
            Subcity = request.Subcity,
            Woreda = request.Woreda,
            HouseNo = request.HouseNo,
            LabourId = request.LabourId,
            CountryOfTravel = request.CountryOfTravel ?? request.Placement?.CountryOfTravel ?? request.Placement?.WorksIn,
            OfficeName = request.OfficeName ?? office.Name,
            ContractDate = ParseDate(request.ContractDate) ?? ParseDate(request.Placement?.ContractDate),
            OfficeId = request.OfficeId,
            Status = CandidateStatus.Active,
            CurrentStageId = initialStage?.Id,
            CurrentStageName = initialStage?.Name,
            CurrentStatusValues = JsonDocument.Parse("{}"),
            RegisteredAt = DateTime.UtcNow,
            RegisteredBy = _currentUser.UserName
        };

        if (request.Placement is not null || request.CountryOfTravel is not null)
        {
            candidate.Placement = new CandidatePlacement
            {
                CountryOfTravel = request.Placement?.CountryOfTravel ?? request.CountryOfTravel,
                WorksIn = request.Placement?.WorksIn ?? request.CountryOfTravel,
                VisaNumber = request.Placement?.VisaNumber,
                VisaType = request.Placement?.VisaType,
                SponsorId = request.Placement?.SponsorId,
                SponsorName = request.Placement?.SponsorName,
                SponsorPhone = request.Placement?.SponsorPhone,
                Agent = request.Placement?.Agent,
                ContractNumber = request.Placement?.ContractNumber,
                ContractDate = ParseDate(request.Placement?.ContractDate) ?? candidate.ContractDate
            };
        }

        if (request.Relative is not null && !string.IsNullOrWhiteSpace(request.Relative.RelativeName))
        {
            candidate.Relatives.Add(new CandidateRelative
            {
                RelativeName = request.Relative.RelativeName,
                RelativePhone = request.Relative.RelativePhone,
                RelativeKinship = request.Relative.RelativeKinship
            });
        }

        if (request.Skills is not null)
        {
            candidate.Skills = new CandidateSkills
            {
                EnglishLevel = request.Skills.EnglishLevel,
                ArabicLevel = request.Skills.ArabicLevel,
                CanIron = request.Skills.CanIron ?? false,
                CanSew = request.Skills.CanSew ?? false,
                CanBabysit = request.Skills.CanBabysit ?? false,
                CanChildcare = request.Skills.CanChildcare ?? false,
                CanArabicCooking = request.Skills.CanArabicCooking ?? false,
                CanClean = request.Skills.CanClean ?? false,
                CanWash = request.Skills.CanWash ?? false,
                CanCook = request.Skills.CanCook ?? false
            };
        }

        candidate.AddDomainEvent(new CandidateRegisteredEvent(
            candidate.Id, candidate.FullName, candidate.OfficeId, initialStage?.Id ?? Guid.Empty));

        _context.Candidates.Add(candidate);
        await _context.SaveChangesAsync(cancellationToken);

        await _workflowEngine.InitializeCandidateAsync(candidate, cancellationToken);

        return Result<Guid>.Success(candidate.Id, 201);
    }

    private static DateOnly? ParseDate(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : DateOnly.Parse(value);
}
