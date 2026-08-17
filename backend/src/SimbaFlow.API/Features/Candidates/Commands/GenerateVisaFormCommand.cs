using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Candidates.Commands;

public record GenerateVisaFormCommand(Guid CandidateId) : IRequest<Result<byte[]>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class GenerateVisaFormHandler : IRequestHandler<GenerateVisaFormCommand, Result<byte[]>>
{
    private readonly ITenantDbContext _context;
    private readonly ICvGenerationService _cvGeneration;
    private readonly IFileStorageService _fileStorage;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUser;

    public GenerateVisaFormHandler(
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

    public async Task<Result<byte[]>> Handle(GenerateVisaFormCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, cancellationToken);

        if (candidate is null)
            return Result<byte[]>.Failure("Candidate not found", 404);

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

        var pdfBytes = await _cvGeneration.GenerateVisaFormAsync(candidate, photoBytes, cancellationToken);

        var tenantSlug = _tenantContext.SchemaName ?? "default";
        await using var pdfStream = new MemoryStream(pdfBytes);
        var relativePath = await _fileStorage.UploadAsync(
            tenantSlug,
            candidate.Id,
            $"visa_{candidate.PassportNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
            "application/pdf",
            pdfStream,
            cancellationToken);

        _context.CandidateDocuments.Add(new CandidateDocument
        {
            CandidateId = candidate.Id,
            FileName = Path.GetFileName(relativePath),
            OriginalFileName = $"VisaForm_{candidate.FullName.Replace(' ', '_')}.pdf",
            ContentType = "application/pdf",
            FilePath = relativePath,
            DocumentType = DocumentType.VisaForm,
            FileSizeBytes = pdfBytes.Length,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = _currentUser.UserName
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<byte[]>.Success(pdfBytes);
    }
}
