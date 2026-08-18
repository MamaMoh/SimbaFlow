using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Partners;

/// <summary>
/// A signed contract (or amendment) attached to a partner agreement.
///
/// Lives in the public schema alongside PartnerLink rather than in the tenant schema, because the
/// agreement itself is stored there. TenantId is carried explicitly so a document can never be
/// read across agencies — the link id alone is not a tenant boundary.
/// </summary>
public class PartnerAgreementDocument : BaseEntity
{
    public Guid PartnerLinkId { get; set; }
    public PartnerLink? PartnerLink { get; set; }

    /// <summary>Owning agency. Every read must filter on this.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Name on disk (generated, not user-supplied).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Name the user uploaded, shown in the UI.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    /// <summary>Free-text label, e.g. "Signed contract" or "Addendum 1".</summary>
    public string? Title { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public string? UploadedBy { get; set; }
}
