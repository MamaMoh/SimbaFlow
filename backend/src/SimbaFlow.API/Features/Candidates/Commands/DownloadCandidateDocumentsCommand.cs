using System.IO.Compression;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Candidates.Commands;

/// <summary>
/// The selected candidates' selected paperwork: one PDF per candidate.
///
/// This is the job the desk actually has: an embassy run needs the passport, the photo and the CV
/// for twenty people, and collecting that a document at a time is twenty times three clicks plus
/// twenty trips into a candidate's page.
///
/// Each candidate's paperwork is one PDF — their CV, passport and photos together, in that order —
/// and several candidates come back as a ZIP of those, one file per person named after them.
/// Everyone was merged into a single PDF with a divider page between people, on the idea that the
/// packet is printed and stapled as one. It is not: each candidate's file goes to a partner on its
/// own, so the desk was splitting the stack back apart by hand. One candidate is still one PDF,
/// with no ZIP around it.
/// </summary>
public record DownloadCandidateDocumentsCommand(
    List<Guid> CandidateIds,
    List<int> DocumentTypes) : IRequest<Result<CandidateDocumentsDownload>>, IRequirePermission, IAuditedRead
{
    public string RequiredPermission => "candidate.read";
    public string AuditEntityType => "Candidate";
    public IReadOnlyCollection<Guid> AuditEntityIds => CandidateIds;
}

/// <summary>What the download is, as the browser should save it.</summary>
public sealed record CandidateDocumentsDownload(byte[] Content, string ContentType, string FileName);

public class DownloadCandidateDocumentsHandler
    : IRequestHandler<DownloadCandidateDocumentsCommand, Result<CandidateDocumentsDownload>>
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

    public async Task<Result<CandidateDocumentsDownload>> Handle(
        DownloadCandidateDocumentsCommand request, CancellationToken ct)
    {
        var ids = (request.CandidateIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(MaxCandidates)
            .ToList();

        if (ids.Count == 0)
            return Result<CandidateDocumentsDownload>.Failure("Select at least one candidate.", 400);

        var types = (request.DocumentTypes ?? [])
            .Where(t => Enum.IsDefined(typeof(DocumentType), t))
            .Select(t => (DocumentType)t)
            .Distinct()
            .ToList();

        if (types.Count == 0)
            return Result<CandidateDocumentsDownload>.Failure("Choose at least one kind of document.", 400);

        var candidates = await _context.Candidates
            .AsNoTracking()
            // A CV can be produced here, and it prints every posting.
            .Include(c => c.WorkExperiences)
            .Where(c => ids.Contains(c.Id) && !c.IsDeleted)
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return Result<CandidateDocumentsDownload>.Failure("No candidates found.", 404);

        var documents = await _context.CandidateDocuments
            .AsNoTracking()
            .Where(d => ids.Contains(d.CandidateId) && types.Contains(d.DocumentType))
            .ToListAsync(ct);

        var byCandidate = documents
            .GroupBy(d => d.CandidateId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var wantsCv = types.Contains(DocumentType.CV);
        var files = new List<(Candidate Candidate, byte[] Pdf)>();

        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();

            var items = await PagesForAsync(
                candidate, byCandidate.GetValueOrDefault(candidate.Id) ?? [], wantsCv, ct);

            // A candidate with none of the chosen documents is left out rather than given an
            // empty file: the folder should hold what there is to send.
            var pdf = await _pdfBundle.MergeAsync(items, ct);
            if (pdf is not null) files.Add((candidate, pdf));
        }

        if (files.Count == 0)
        {
            return Result<CandidateDocumentsDownload>.Failure(
                "None of the selected candidates have any of those documents yet.", 404);
        }

        // Asked for one person, handed one PDF — a ZIP around a single file is one more thing
        // to open before the print dialog.
        if (candidates.Count == 1)
        {
            var (only, pdf) = files[0];
            return Result<CandidateDocumentsDownload>.Success(new CandidateDocumentsDownload(
                pdf, "application/pdf", DocumentFileName.For(only.FullName, "Documents")));
        }

        return Result<CandidateDocumentsDownload>.Success(new CandidateDocumentsDownload(
            await ZipAsync(files, ct),
            "application/zip",
            $"Documents {DateTime.UtcNow:yyyy-MM-dd}.zip"));
    }

    /// <summary>
    /// One candidate's paperwork, in the order it is sent: the CV first, then the passport, the
    /// photos and the rest, the same order for everyone.
    /// </summary>
    private async Task<List<PdfBundleItem>> PagesForAsync(
        Candidate candidate, IReadOnlyList<CandidateDocument> onFile, bool wantsCv, CancellationToken ct)
    {
        var items = new List<PdfBundleItem>();

        // The CV leads, and is drawn rather than read — see RenderCvAsync.
        if (wantsCv)
        {
            var cv = await RenderCvAsync(candidate, onFile, ct);
            if (cv is not null)
                items.Add(PdfBundleItem.Document($"{candidate.FullName} · CV", cv));
        }

        var found = Preferred(onFile)
            .Where(d => d.DocumentType != DocumentType.CV)
            // Every candidate's pages come in the same order, so a file can be checked by flicking
            // through rather than reading each page.
            .OrderBy(d => RankOf(d.DocumentType))
            .ThenBy(d => d.UploadedAt);

        foreach (var doc in found)
        {
            var bytes = await ReadAsync(doc.FilePath, ct);

            // The row survived but the file did not. Skipped rather than failed: the rest of the
            // candidate's file is still worth sending.
            if (bytes is null) continue;

            items.Add(PdfBundleItem.Document(
                $"{candidate.FullName} · {doc.DocumentType}", bytes,
                isSnapshot: doc.DocumentType is DocumentType.Passport or DocumentType.FullPhoto,
                isPortrait: doc.DocumentType is DocumentType.Photo));
        }

        return items;
    }

    /// <summary>
    /// One PDF per candidate, each named after the person: "Almaz Debela Kebede - Documents.pdf".
    ///
    /// Two candidates can share a name, and a ZIP with two entries called the same thing extracts
    /// to one file. The passport number settles it, but only for the second one — putting it on
    /// every entry would make the common case harder to read for a problem it does not have.
    /// </summary>
    private static async Task<byte[]> ZipAsync(
        IReadOnlyList<(Candidate Candidate, byte[] Pdf)> files, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (candidate, pdf) in files)
            {
                var name = DocumentFileName.For(candidate.FullName, "Documents");
                if (!taken.Add(name))
                {
                    name = DocumentFileName.For(
                        $"{candidate.FullName} {candidate.PassportNumber}", "Documents");
                    taken.Add(name);
                }

                // PDFs barely compress; spending time trying only delays the download.
                var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
                await using var stream = entry.Open();
                await stream.WriteAsync(pdf, ct);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// The copies worth printing, where a candidate has more than one of a kind.
    ///
    /// A signed contract beats the one we drew. Once both exist — the contract was generated
    /// before the parties sent theirs back, or the desk printed ours and then filed the signed
    /// scan — the packet would otherwise carry two contracts that disagree, and the clerk has no
    /// way to tell which is in force. The uploaded one is.
    ///
    /// Only where both kinds exist. Several uploads of the same kind are several documents and
    /// all of them belong in the packet: a passport has pages, and "Other" is whatever the desk
    /// decided to keep.
    /// </summary>
    private static IEnumerable<CandidateDocument> Preferred(IEnumerable<CandidateDocument> onFile) =>
        onFile
            .GroupBy(d => d.DocumentType)
            .SelectMany(kind => kind.Any(d => !d.IsGenerated)
                ? kind.Where(d => !d.IsGenerated)
                : kind);

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
