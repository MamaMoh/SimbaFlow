using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
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

    /// <summary>
    /// The order each candidate's pages come out in.
    ///
    /// It is the order the download dialog lists the kinds in, which is the order the desk
    /// assembles the packet in — CV on top, then the passport and the photos, then the rest.
    /// Sorting on DocumentType instead ordered the packet by the order the kinds happened to be
    /// added to the enum, which puts the passport (0) ahead of the CV (3) and scatters the rest.
    ///
    /// Keep in step with DOCUMENT_KINDS in
    /// frontend/components/candidates/download-documents-dialog.tsx.
    /// </summary>
    private static readonly DocumentType[] PageOrder =
    [
        DocumentType.CV,
        DocumentType.Passport,
        DocumentType.Photo,
        DocumentType.FullPhoto,
        DocumentType.Contract,
        DocumentType.VisaForm,
        DocumentType.MedicalCertificate,
        DocumentType.LMIS,
        DocumentType.TasheerDocument,
        DocumentType.TicketBooking,
        DocumentType.Other,
    ];

    private static readonly Dictionary<DocumentType, int> PageRank =
        PageOrder.Select((type, index) => (type, index))
            .ToDictionary(x => x.type, x => x.index);

    /// <summary>A kind added to the enum but not to the list above sorts last, not first.</summary>
    private static int RankOf(DocumentType type) =>
        PageRank.TryGetValue(type, out var rank) ? rank : PageOrder.Length;

    private readonly ITenantDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly ICvGenerationService _cvGeneration;
    private readonly IPdfBundleService _pdfBundle;
    private readonly ILogger<DownloadCandidateDocumentsHandler> _logger;

    public DownloadCandidateDocumentsHandler(
        ITenantDbContext context,
        IFileStorageService fileStorage,
        ICvGenerationService cvGeneration,
        IPdfBundleService pdfBundle,
        ILogger<DownloadCandidateDocumentsHandler> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _cvGeneration = cvGeneration;
        _pdfBundle = pdfBundle;
        _logger = logger;
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

            var onFile = byCandidate.GetValueOrDefault(candidate.Id) ?? [];

            // The CV leads, and is drawn rather than read — see RenderCvAsync.
            if (wantsCv)
            {
                var cv = await RenderCvAsync(candidate, onFile, ct);
                if (cv is not null)
                    items.Add(PdfBundleItem.Document($"{candidate.FullName} · CV", cv));
            }

            var found = onFile
                .Where(d => d.DocumentType != DocumentType.CV)
                // Every candidate's pages come in the same order, so a stack of twenty can be
                // checked by flicking through rather than reading each page.
                .OrderBy(d => RankOf(d.DocumentType))
                .ThenBy(d => d.UploadedAt);

            foreach (var doc in found)
            {
                var bytes = await ReadAsync(doc.FilePath, ct);

                // The row survived but the file did not. Skipped rather than failed: the rest of
                // the packet is still worth printing.
                if (bytes is null) continue;

                items.Add(PdfBundleItem.Document(
                    $"{candidate.FullName} · {doc.DocumentType}", bytes));
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

    /// <summary>
    /// This candidate's CV, drawn now, in the layout the agency has chosen.
    ///
    /// Drawn rather than taken from file, even when a copy is on file. A stored CV is a snapshot of
    /// whichever layout was selected the day someone last pressed the button, so a packet assembled
    /// from stored copies is a packet of whatever layouts happen to be lying around rather than the
    /// agency's own — and because every press used to file a fresh row, one candidate can have
    /// several, which arrived as several CVs in several layouts. Drawing it here is also what makes
    /// it possible to put the CV first: it is the one page in the packet that does not have to
    /// exist yet.
    /// </summary>
    private async Task<byte[]?> RenderCvAsync(
        Candidate candidate, IReadOnlyList<CandidateDocument> onFile, CancellationToken ct)
    {
        try
        {
            return await _cvGeneration.GenerateAsync(
                candidate,
                await ReadAsync(candidate.PhotoPath, ct),
                await ReadAsync(candidate.FullPhotoPath, ct),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A photo the renderer cannot open should cost this candidate their CV page, not the
            // other nineteen people theirs. The newest copy on file is a stale layout but a real
            // document, which beats a hole in the packet.
            _logger.LogWarning(
                ex, "Could not draw a CV for candidate {CandidateId}; falling back to the copy on file",
                candidate.Id);

            foreach (var doc in onFile
                .Where(d => d.DocumentType == DocumentType.CV)
                .OrderByDescending(d => d.UploadedAt))
            {
                var bytes = await ReadAsync(doc.FilePath, ct);
                if (bytes is not null) return bytes;
            }

            return null;
        }
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
