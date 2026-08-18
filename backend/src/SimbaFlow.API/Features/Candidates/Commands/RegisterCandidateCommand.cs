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
    string? PartnerName,
    Guid? PartnerAgencyId,
    string? ContractDate,
    CandidateIntakePayload? Intake = null) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "candidate.create";
}

public class RegisterCandidateHandler : IRequestHandler<RegisterCandidateCommand, Result<Guid>>
{
    private readonly ITenantDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPlatformDbContext _platform;

    public RegisterCandidateHandler(
        ITenantDbContext context,
        ICurrentUserService currentUser,
        IPlatformDbContext platform)
    {
        _context = context;
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
            PartnerName = request.PartnerName,
            PartnerAgencyId = request.PartnerAgencyId,
            ContractDate = string.IsNullOrEmpty(request.ContractDate) ? null : DateOnly.Parse(request.ContractDate),
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
            candidate.Id, candidate.FullName, initialStage?.Id ?? Guid.Empty));

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

    private async Task<string> GenerateApplicationNoAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow;
        var prefix = $"APP-{today:yyyyMMdd}-";
        var countToday = await _context.Candidates
            .CountAsync(c => c.ApplicationNo != null && c.ApplicationNo.StartsWith(prefix), cancellationToken);
        return $"{prefix}{(countToday + 1):D4}";
    }
}
