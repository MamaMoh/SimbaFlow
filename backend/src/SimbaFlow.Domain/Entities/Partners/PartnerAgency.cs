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

    public string? Notes { get; set; }
}
