using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Partners;

/// <summary>
/// Platform catalog of foreign (receiving-country) partner agencies.
/// Shared across tenants; Art. 40 capacity is enforced on PartnerLink create.
/// </summary>
public class PartnerAgency : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>ISO country code or short country name (e.g. SA, AE, KW).</summary>
    public string CountryCode { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;

    public string? ForeignLicenseId { get; set; }

    public PartnerCapacityTier CapacityTier { get; set; } = PartnerCapacityTier.Medium;

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }
}
