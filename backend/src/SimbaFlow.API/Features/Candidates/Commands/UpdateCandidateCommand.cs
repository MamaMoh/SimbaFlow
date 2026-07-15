using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Candidates.Commands;

public record UpdateCandidateCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? Nationality,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? City,
    string? Country,
    string? LabourId,
    string? CountryOfTravel,
    string? OfficeName,
    string? ContractDate) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "candidate.update";
}

public class UpdateCandidateHandler : IRequestHandler<UpdateCandidateCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdateCandidateHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result> Handle(UpdateCandidateCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, cancellationToken);

        if (candidate is null)
            return Result.Failure("Candidate not found", 404);

        // Check labour ID uniqueness if changed
        if (!string.IsNullOrEmpty(request.LabourId) && request.LabourId != candidate.LabourId)
        {
            var labourIdExists = await _context.Candidates
                .AnyAsync(c => c.LabourId == request.LabourId && c.Id != request.Id && !c.IsDeleted, cancellationToken);
            if (labourIdExists)
                return Result.Failure("A candidate with this Labour ID already exists.", 409);
        }

        candidate.FirstName = request.FirstName;
        candidate.LastName = request.LastName;
        candidate.MiddleName = request.MiddleName;
        candidate.Nationality = request.Nationality;
        candidate.PhoneNumber = request.PhoneNumber;
        candidate.Email = request.Email;
        candidate.Address = request.Address;
        candidate.City = request.City;
        candidate.Country = request.Country;
        candidate.LabourId = request.LabourId;
        candidate.CountryOfTravel = request.CountryOfTravel;
        candidate.OfficeName = request.OfficeName;
        candidate.ContractDate = string.IsNullOrEmpty(request.ContractDate) ? null : DateOnly.Parse(request.ContractDate);

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
    private readonly IApplicationDbContext _context;

    public DeleteCandidateHandler(IApplicationDbContext context) => _context = context;

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
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public UploadDocumentHandler(IApplicationDbContext context, IFileStorageService fileStorage)
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

        // Upload file
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
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(doc.Id);
    }
}
