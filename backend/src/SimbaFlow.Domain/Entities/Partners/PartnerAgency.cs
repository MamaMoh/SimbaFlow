using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Partners;

/// <summary>
/// Platform catalog of foreign (receiving-country) partner agencies.
/// Shared across tenants. Foreign partners have no cap on how many Ethiopian agencies they
/// work with; the only regulatory cap is on the Ethiopian side, by agency level.
/// </summary>
public class PartnerAgency : BaseEntity
{
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
    /// Relative storage path of the partner's letterhead logo.
    ///
    /// Candidates placed with this partner get their documents headed with it, because the paper
    /// is presented to the embassy under the partner's name, not ours. This catalog entry is
    /// shared, so the logo is the partner's own branding rather than any one agency's copy of it.
    /// </summary>
    public string? LogoPath { get; set; }

    public string? Notes { get; set; }
}
