using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Candidates;

/// <summary>
/// Filing a document the system generated — a CV, a visa form, a contract, a Tasheer letter.
///
/// These are not uploads. An upload is a new fact about the candidate and belongs alongside the
/// ones before it; a generated document is a *rendering* of facts already held, so a second one is
/// not a second document, it is the same document again. Each generator used to append a fresh row
/// and a fresh file on every click, which meant a candidate whose CV had been printed four times
/// had four identical PDFs on disk and four rows in the list — and the Documents tab stopped being
/// a list of the candidate's paperwork and became a log of how often someone pressed a button.
///
/// So the current one replaces the previous one.
/// </summary>
public static class GeneratedDocuments
{
    /// <summary>
    /// Stores <paramref name="bytes"/> as this candidate's document of that type, replacing any
    /// earlier copy. Does not save — the caller commits, so the write stays in its transaction.
    /// </summary>
    public static async Task ReplaceAsync(
        ITenantDbContext context,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        string tenantSlug,
        Candidate candidate,
        DocumentType type,
        string fileName,
        string displayName,
        byte[] bytes,
        CancellationToken ct)
    {
        var superseded = await context.CandidateDocuments
            .Where(d => d.CandidateId == candidate.Id && d.DocumentType == type)
            .ToListAsync(ct);

        await using (var stream = new MemoryStream(bytes))
        {
            var relativePath = await storage.UploadAsync(
                tenantSlug, candidate.Id, fileName, "application/pdf", stream, ct);

            context.CandidateDocuments.Add(new CandidateDocument
            {
                CandidateId = candidate.Id,
                FileName = Path.GetFileName(relativePath),
                OriginalFileName = displayName,
                ContentType = "application/pdf",
                FilePath = relativePath,
                DocumentType = type,
                FileSizeBytes = bytes.Length,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = currentUser.UserName,
            });
        }

        // The new copy is written before the old ones are dropped, so a failure anywhere above
        // leaves the candidate with the document they already had rather than with none.
        context.CandidateDocuments.RemoveRange(superseded);

        foreach (var old in superseded)
        {
            // Best effort: a file that cannot be deleted is wasted space, not a reason to fail a
            // print. The row is gone either way, so it will not be offered to anyone.
            try
            {
                await storage.DeleteAsync(old.FilePath, ct);
            }
            catch (Exception)
            {
                // Swallowed deliberately — see above.
            }
        }
    }
}
