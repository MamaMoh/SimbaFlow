using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.API.Features.Partners;

namespace SimbaFlow.API.Features.Candidates.Commands;

public record UpdateCandidateCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? PassportNumber,
    string? DateOfBirth,
    int? Gender,
    string? Nationality,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? City,
    string? Country,
    string? LabourId,
    string? CountryOfTravel,
    string? PartnerName,
    Guid? PartnerAgencyId = null,
    string? ContractDate = null,
    CandidateIntakePayload? Intake = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "candidate.update";
}

public class UpdateCandidateHandler : IRequestHandler<UpdateCandidateCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IPlatformDbContext _platform;
    private readonly ICurrentUserService _currentUser;

    public UpdateCandidateHandler(
        ITenantDbContext context,
        IPlatformDbContext platform,
        ICurrentUserService currentUser)
    {
        _context = context;
        _platform = platform;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateCandidateCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, cancellationToken);

        if (candidate is null)
            return Result.Failure("Candidate not found", 404);

        // Re-validate only when the partner is actually changing, so an existing candidate whose
        // agreement has since lapsed can still be edited for other reasons.
        if (request.PartnerAgencyId != candidate.PartnerAgencyId)
        {
            var partnerCheck = await PartnerLinkValidator.CheckAsync(
                _platform, _currentUser.TenantId, request.PartnerAgencyId, cancellationToken);
            if (!partnerCheck.IsValid)
                return Result.Failure(partnerCheck.Error!, 400);
        }

        if (!string.IsNullOrEmpty(request.LabourId) && request.LabourId != candidate.LabourId)
        {
            var labourIdExists = await _context.Candidates
                .AnyAsync(c => c.LabourId == request.LabourId && c.Id != request.Id && !c.IsDeleted, cancellationToken);
            if (labourIdExists)
                return Result.Failure("A candidate with this Labour ID already exists.", 409);
        }

        if (!string.IsNullOrWhiteSpace(request.PassportNumber) &&
            !string.Equals(request.PassportNumber, candidate.PassportNumber, StringComparison.OrdinalIgnoreCase))
        {
            var passportExists = await _context.Candidates
                .AnyAsync(c => c.PassportNumber == request.PassportNumber && c.Id != request.Id && !c.IsDeleted, cancellationToken);
            if (passportExists)
                return Result.Failure("A candidate with this passport number already exists.", 409);
            candidate.PassportNumber = request.PassportNumber;
        }

        candidate.FirstName = request.FirstName;
        candidate.LastName = request.LastName;
        candidate.MiddleName = request.MiddleName;
        if (!string.IsNullOrWhiteSpace(request.DateOfBirth) && DateOnly.TryParse(request.DateOfBirth, out var dob))
            candidate.DateOfBirth = dob;
        if (request.Gender.HasValue)
            candidate.Gender = (Gender)request.Gender.Value;
        candidate.Nationality = request.Nationality;
        candidate.PhoneNumber = request.PhoneNumber;
        candidate.Email = request.Email;
        candidate.Address = request.Address;
        candidate.City = request.City;
        candidate.Country = request.Country;
        candidate.LabourId = request.LabourId;
        candidate.CountryOfTravel = request.CountryOfTravel;
        candidate.PartnerName = request.PartnerName;
        candidate.PartnerAgencyId = request.PartnerAgencyId;
        candidate.ContractDate = string.IsNullOrEmpty(request.ContractDate) ? null : DateOnly.Parse(request.ContractDate);

        if (request.Intake is not null)
            CandidateIntakeMapper.Apply(candidate, request.Intake);

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public record DeleteCandidateCommand(Guid Id) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "candidate.delete";
}

public class DeleteCandidateHandler : IRequestHandler<DeleteCandidateCommand, Result>
{
    private readonly ITenantDbContext _context;

    public DeleteCandidateHandler(ITenantDbContext context) => _context = context;

    public async Task<Result> Handle(DeleteCandidateCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, cancellationToken);

        if (candidate is null)
            return Result.Failure("Candidate not found", 404);

        candidate.IsDeleted = true;
        candidate.Status = CandidateStatus.Archived;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public record UploadDocumentCommand(Guid CandidateId, Microsoft.AspNetCore.Http.IFormFile File, int DocumentType) : IRequest<Result<Guid>>;

public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, Result<Guid>>
{
    private readonly ITenantDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public UploadDocumentHandler(ITenantDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    public async Task<Result<Guid>> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, cancellationToken);

        if (candidate is null)
            return Result<Guid>.Failure("Candidate not found", 404);

        var relativePath = await _fileStorage.UploadAsync(
            "default", request.CandidateId,
            request.File.FileName, request.File.ContentType,
            request.File.OpenReadStream(), cancellationToken);

        var doc = new Domain.Entities.Candidates.CandidateDocument
        {
            CandidateId = request.CandidateId,
            FileName = System.IO.Path.GetFileName(relativePath),
            OriginalFileName = request.File.FileName,
            ContentType = request.File.ContentType,
            FilePath = relativePath,
            DocumentType = (DocumentType)request.DocumentType,
            FileSizeBytes = request.File.Length,
            UploadedAt = DateTime.UtcNow,
        };

        _context.CandidateDocuments.Add(doc);

        if (doc.DocumentType == DocumentType.Photo)
            candidate.PhotoPath = relativePath;
        else if (doc.DocumentType == DocumentType.FullPhoto)
            candidate.FullPhotoPath = relativePath;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(doc.Id);
    }
}
