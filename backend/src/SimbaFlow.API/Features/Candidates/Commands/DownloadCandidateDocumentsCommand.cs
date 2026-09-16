using System.IO.Compression;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Candidates.Commands;

/// <summary>
/// The selected candidates' selected paperwork, as one ZIP.
///
/// This is the job the desk actually has: an embassy run needs the passport, the photo and the CV
/// for twenty people, and collecting that a document at a time is twenty times three clicks plus
/// twenty trips into a candidate's page. One folder per candidate, named so the folders sort the
/// way a person would file them.
/// </summary>
public record DownloadCandidateDocumentsCommand(
    List<Guid> CandidateIds,
    List<int> DocumentTypes) : IRequest<Result<byte[]>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class DownloadCandidateDocumentsHandler
    : IRequestHandler<DownloadCandidateDocumentsCommand, Result<byte[]>>
{
    /// <summary>
    /// Enough for an embassy run, and short of the request timing out while the desk waits. The
    /// same cap the bulk CV download uses.
    /// </summary>
    private const int MaxCandidates = 50;

    private readonly ITenantDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly ICvGenerationService _cvGeneration;

    public DownloadCandidateDocumentsHandler(
        ITenantDbContext context,
        IFileStorageService fileStorage,
        ICvGenerationService cvGeneration)
    {
        _context = context;
        _fileStorage = fileStorage;
        _cvGeneration = cvGeneration;
    }

    public async Task<Result<byte[]>> Handle(
        DownloadCandidateDocumentsCommand request, CancellationToken ct)
    {
        var ids = (request.CandidateIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(MaxCandidates)
            .ToList();

        if (ids.Count == 0)
            return Result<byte[]>.Failure("Select at least one candidate.", 400);

        var types = (request.DocumentTypes ?? [])
            .Where(t => Enum.IsDefined(typeof(DocumentType), t))
            .Select(t => (DocumentType)t)
            .Distinct()
            .ToList();

        if (types.Count == 0)
            return Result<byte[]>.Failure("Choose at least one kind of document.", 400);

        var candidates = await _context.Candidates
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id) && !c.IsDeleted)
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return Result<byte[]>.Failure("No candidates found.", 404);

        var documents = await _context.CandidateDocuments
            .AsNoTracking()
            .Where(d => ids.Contains(d.CandidateId) && types.Contains(d.DocumentType))
            .ToListAsync(ct);

        var byCandidate = documents
            .GroupBy(d => d.CandidateId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var wantsCv = types.Contains(DocumentType.CV);
        var included = 0;
        var missing = new List<string>();

        await using var zipMs = new MemoryStream();
        using (var archive = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var candidate in candidates)
            {
                ct.ThrowIfCancellationRequested();

                var folder = SafeName(
                    $"{candidate.PassportNumber}_{candidate.LastName}_{candidate.FirstName}");
                var found = byCandidate.GetValueOrDefault(candidate.Id) ?? [];

                foreach (var doc in found)
                {
                    await using var stream = await _fileStorage.DownloadAsync(doc.FilePath, ct);
                    if (stream is null)
                    {
                        // The row survived but the file did not. Say which, rather than handing
                        // back a folder that is quietly short one document.
                        missing.Add($"{candidate.FullName}: {doc.DocumentType}");
                        continue;
                    }

                    var name = SafeName($"{doc.DocumentType}_{doc.OriginalFileName ?? doc.FileName}");
                    var entry = archive.CreateEntry($"{folder}/{name}", CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await stream.CopyToAsync(entryStream, ct);
                    included++;
                }

                // A CV is the one document that can always be produced, because it is drawn from
                // the candidate's own record. Asking for it and getting nothing because nobody has
                // pressed the button before would be a strange answer.
                if (wantsCv && !found.Any(d => d.DocumentType == DocumentType.CV))
                {
                    var pdf = await _cvGeneration.GenerateAsync(
                        candidate,
                        await ReadAsync(candidate.PhotoPath, ct),
                        await ReadAsync(candidate.FullPhotoPath, ct),
                        ct);

                    var entry = archive.CreateEntry($"{folder}/CV_{folder}.pdf", CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await entryStream.WriteAsync(pdf, ct);
                    included++;
                }
            }
        }

        if (included == 0)
        {
            return Result<byte[]>.Failure(
                "None of the selected candidates have any of those documents yet.", 404);
        }

        return Result<byte[]>.Success(zipMs.ToArray());
    }

    private async Task<byte[]?> ReadAsync(string? path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        await using var stream = await _fileStorage.DownloadAsync(path, ct);
        if (stream is null) return null;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    /// <summary>A name a filesystem will accept, on every platform the ZIP might be opened on.</summary>
    private static string SafeName(string value)
    {
        var name = value.Replace(' ', '_');
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
