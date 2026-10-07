using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// File reference for a candidate document stored on the server file system.
/// </summary>
public class CandidateDocument : BaseEntity
{
    public Guid CandidateId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public DocumentType DocumentType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? UploadedBy { get; set; }

    /// <summary>
    /// Whether this document was drawn by us rather than handed to us.
    ///
    /// The difference matters in both directions. A generated document is a rendering of facts
    /// already held, so printing it again replaces the last copy — but it must only replace
    /// *other* generated copies, or pressing Generate destroys the signed original someone
    /// uploaded. And a contract that exists because the parties signed it is the contract: asked
    /// for it, we hand back that file rather than drawing a fresh one that nobody has signed.
    /// </summary>
    public bool IsGenerated { get; set; }

    // Navigation
    public Candidate? Candidate { get; set; }
}
