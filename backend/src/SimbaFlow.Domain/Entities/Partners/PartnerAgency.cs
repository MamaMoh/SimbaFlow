using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Partners;

/// <summary>
/// A foreign (receiving-country) partner agency, belonging to the Ethiopian agency that
/// registered it.
///
/// This started as one catalog shared across the platform, on the reasoning that the same Saudi
/// recruiter is the same company whoever is looking at it. That is true of the company and false
/// of the record: who you work with, on what terms, is the agency's own commercial business, and
/// a shared catalog put every agency's partner list in front of every competitor on the platform.
/// Each row now belongs to whoever registered it.
///
/// Foreign partners still have no cap on how many Ethiopian agencies they work with; the only
/// regulatory cap is on the Ethiopian side, by agency level.
/// </summary>
public class PartnerAgency : BaseEntity
{
    /// <summary>
    /// The agency this partner belongs to. Null only for rows the platform itself seeded, which
    /// no agency sees in its own list.
    /// </summary>
    public Guid? OwnerTenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>ISO country code or short country name (e.g. SA, AE, KW).</summary>
    public string CountryCode { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;

    public string? ForeignLicenseId { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Relative storage path of the partner's printed letterhead — the wide banner that heads a
    /// document, as distinct from the mark in <see cref="LogoPath"/>.
    /// </summary>
    public string? LetterheadPath { get; set; }

    /// <summary>
    /// Relative storage path of the partner's logo.
    ///
    /// Candidates placed with this partner get their documents headed with it, because the paper
    /// is presented to the embassy under the partner's name, not ours. This catalog entry is
    /// shared, so the logo is the partner's own branding rather than any one agency's copy of it.
    /// </summary>
    public string? LogoPath { get; set; }

    public string? Notes { get; set; }
}
