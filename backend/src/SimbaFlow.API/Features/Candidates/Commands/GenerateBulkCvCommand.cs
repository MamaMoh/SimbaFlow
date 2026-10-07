using System.IO.Compression;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

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
        // Two candidates can share a name, and a zip with two entries called the same thing
        // extracts to one file. The passport number settles it, but only for the second one —
        // putting it on every entry would make the common case harder to read for a problem the
        // common case does not have.
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                var name = DocumentFileName.For(candidate.FullName, "CV");
                if (!taken.Add(name))
                {
                    name = DocumentFileName.For(
                        $"{candidate.FullName} {candidate.PassportNumber}", "CV");
                    taken.Add(name);
                }

                var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdf, cancellationToken);
            }
        }

        return Result<byte[]>.Success(zipMs.ToArray());
    }
}
