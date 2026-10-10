using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Candidates.Commands;

public record GenerateCVCommand(Guid CandidateId) : IRequest<Result<GeneratedPdf>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class GenerateCVHandler : IRequestHandler<GenerateCVCommand, Result<GeneratedPdf>>
{
    private readonly ITenantDbContext _context;
    private readonly ICvGenerationService _cvGeneration;
    private readonly IFileStorageService _fileStorage;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUser;

    public GenerateCVHandler(
        ITenantDbContext context,
        ICvGenerationService cvGeneration,
        IFileStorageService fileStorage,
        ITenantContext tenantContext,
        ICurrentUserService currentUser)
    {
        _context = context;
        _cvGeneration = cvGeneration;
        _fileStorage = fileStorage;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Result<GeneratedPdf>> Handle(GenerateCVCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _context.Candidates
            // The CV prints every posting, not the summary columns.
            .Include(c => c.WorkExperiences)
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, cancellationToken);

        if (candidate is null)
            return Result<GeneratedPdf>.Failure("Candidate not found", 404);

        var downloadName = DocumentFileName.For(candidate.FullName, "CV");

        byte[]? photoBytes = null;
        if (!string.IsNullOrWhiteSpace(candidate.PhotoPath))
        {
            await using var photoStream = await _fileStorage.DownloadAsync(candidate.PhotoPath, cancellationToken);
            if (photoStream is not null)
            {
                using var ms = new MemoryStream();
                await photoStream.CopyToAsync(ms, cancellationToken);
                photoBytes = ms.ToArray();
            }
        }

        byte[]? fullPhotoBytes = null;
        if (!string.IsNullOrWhiteSpace(candidate.FullPhotoPath))
        {
            await using var fullStream = await _fileStorage.DownloadAsync(candidate.FullPhotoPath, cancellationToken);
            if (fullStream is not null)
            {
                using var ms = new MemoryStream();
                await fullStream.CopyToAsync(ms, cancellationToken);
                fullPhotoBytes = ms.ToArray();
            }
        }

        var pdfBytes = await _cvGeneration.GenerateAsync(candidate, photoBytes, fullPhotoBytes, cancellationToken);

        var tenantSlug = _tenantContext.SchemaName ?? "default";
        await GeneratedDocuments.ReplaceAsync(
            _context, _fileStorage, _currentUser, tenantSlug, candidate,
            DocumentType.CV,
            $"cv_{candidate.PassportNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
            downloadName,
            pdfBytes,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<GeneratedPdf>.Success(new GeneratedPdf(pdfBytes, downloadName));
    }
}
