using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Candidates.Commands;

/// <summary>
/// The selected candidates' selected paperwork, as one PDF.
///
/// This is the job the desk actually has: an embassy run needs the passport, the photo and the CV
/// for twenty people, and collecting that a document at a time is twenty times three clicks plus
/// twenty trips into a candidate's page. It comes back as a single document because the packet is
/// printed and stapled as one — a folder of loose files means one print dialog per file. Each
/// candidate's pages open with a divider carrying their name, so the stack can be split by hand
/// afterwards.
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
    private readonly IPdfBundleService _pdfBundle;

    public DownloadCandidateDocumentsHandler(
        ITenantDbContext context,
        IFileStorageService fileStorage,
        ICvGenerationService cvGeneration,
        IPdfBundleService pdfBundle)
    {
        _context = context;
        _fileStorage = fileStorage;
        _cvGeneration = cvGeneration;
        _pdfBundle = pdfBundle;
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
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
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
        var items = new List<PdfBundleItem>();

        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();

            // Only worth a divider when there is more than one name in the stack.
            if (candidates.Count > 1)
                items.Add(PdfBundleItem.Divider(candidate.FullName));

            var found = (byCandidate.GetValueOrDefault(candidate.Id) ?? [])
                // Every candidate's pages come in the same order, so a stack of twenty can be
                // checked by flicking through rather than reading each page.
                .OrderBy(d => d.DocumentType)
                .ThenBy(d => d.UploadedAt)
                .ToList();

            foreach (var doc in found)
            {
                var bytes = await ReadAsync(doc.FilePath, ct);

                // The row survived but the file did not. Skipped rather than failed: the rest of
                // the packet is still worth printing.
                if (bytes is null) continue;

                items.Add(PdfBundleItem.Document(
                    $"{candidate.FullName} · {doc.DocumentType}", bytes));
            }

            // A CV is the one document that can always be produced, because it is drawn from the
            // candidate's own record. Asking for it and getting nothing because nobody has pressed
            // the button before would be a strange answer.
            if (wantsCv && !found.Any(d => d.DocumentType == DocumentType.CV))
            {
                items.Add(PdfBundleItem.Document(
                    $"{candidate.FullName} · CV",
                    await _cvGeneration.GenerateAsync(
                        candidate,
                        await ReadAsync(candidate.PhotoPath, ct),
                        await ReadAsync(candidate.FullPhotoPath, ct),
                        ct)));
            }
        }

        var pdf = await _pdfBundle.MergeAsync(items, ct);

        if (pdf is null)
        {
            return Result<byte[]>.Failure(
                "None of the selected candidates have any of those documents yet.", 404);
        }

        return Result<byte[]>.Success(pdf);
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
}
