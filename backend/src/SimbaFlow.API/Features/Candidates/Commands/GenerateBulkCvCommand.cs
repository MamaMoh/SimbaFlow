using System.IO.Compression;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Candidates.Commands;

public record GenerateBulkCvCommand(List<Guid> CandidateIds) : IRequest<Result<byte[]>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class GenerateBulkCvHandler : IRequestHandler<GenerateBulkCvCommand, Result<byte[]>>
{
    private readonly ITenantDbContext _context;
    private readonly ICvGenerationService _cvGeneration;
    private readonly IFileStorageService _fileStorage;

    public GenerateBulkCvHandler(
        ITenantDbContext context,
        ICvGenerationService cvGeneration,
        IFileStorageService fileStorage)
    {
        _context = context;
        _cvGeneration = cvGeneration;
        _fileStorage = fileStorage;
    }

    public async Task<Result<byte[]>> Handle(GenerateBulkCvCommand request, CancellationToken cancellationToken)
    {
        var ids = (request.CandidateIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(50)
            .ToList();

        if (ids.Count == 0)
            return Result<byte[]>.Failure("Select at least one candidate", 400);

        var candidates = await _context.Candidates
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id) && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return Result<byte[]>.Failure("No candidates found", 404);

        await using var zipMs = new MemoryStream();
        using (var archive = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

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

                var pdf = await _cvGeneration.GenerateAsync(candidate, photoBytes, fullPhotoBytes, cancellationToken);
                var safeName = $"{candidate.PassportNumber}_{candidate.LastName}_{candidate.FirstName}"
                    .Replace(" ", "_");
                foreach (var c in Path.GetInvalidFileNameChars())
                    safeName = safeName.Replace(c, '_');

                var entry = archive.CreateEntry($"cv_{safeName}.pdf", CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdf, cancellationToken);
            }
        }

        return Result<byte[]>.Success(zipMs.ToArray());
    }
}
