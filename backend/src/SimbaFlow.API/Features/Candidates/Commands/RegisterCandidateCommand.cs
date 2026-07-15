using MediatR;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Events;
using Microsoft.EntityFrameworkCore;

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
    string? ContractDate,
    Guid OfficeId) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "candidate.create";
}

public class RegisterCandidateHandler : IRequestHandler<RegisterCandidateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RegisterCandidateHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(RegisterCandidateCommand request, CancellationToken cancellationToken)
    {
        // Check passport uniqueness
        var passportExists = await _context.Candidates
            .AnyAsync(c => c.PassportNumber == request.PassportNumber && !c.IsDeleted, cancellationToken);

        if (passportExists)
            return Result<Guid>.Failure("A candidate with this passport number already exists.", 409);

        // Check labour ID uniqueness
        if (!string.IsNullOrEmpty(request.LabourId))
        {
            var labourIdExists = await _context.Candidates
                .AnyAsync(c => c.LabourId == request.LabourId && !c.IsDeleted, cancellationToken);

            if (labourIdExists)
                return Result<Guid>.Failure("A candidate with this Labour ID already exists.", 409);
        }

        // Get initial workflow stage
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
            ContractDate = string.IsNullOrEmpty(request.ContractDate) ? null : DateOnly.Parse(request.ContractDate),
            OfficeId = request.OfficeId,
            Status = CandidateStatus.Active,
            CurrentStageId = initialStage?.Id,
            CurrentStageName = initialStage?.Name,
            RegisteredAt = DateTime.UtcNow,
            RegisteredBy = _currentUser.UserName
        };

        // Emit domain event
        candidate.AddDomainEvent(new CandidateRegisteredEvent(
            candidate.Id, candidate.FullName, candidate.OfficeId, initialStage?.Id ?? Guid.Empty));

        _context.Candidates.Add(candidate);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(candidate.Id, 201);
    }
}
