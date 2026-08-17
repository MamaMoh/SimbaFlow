using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// Agency ደረጃ 1–5 caps from MoLS Directive 1126/2018 Arts. 18–22.
/// </summary>
public static class AgencyLevelRules
{
    public const int MinLevel = 1;
    public const int MaxLevel = 5;

    /// <summary>Max partner agencies per destination country; null MaxCountries = unlimited.</summary>
    public static (int MaxPartnersPerCountry, int? MaxCountries) GetCaps(int agencyLevel) =>
        agencyLevel switch
        {
            1 => (20, null),
            2 => (20, 8),
            3 => (16, 8),
            4 => (8, 4),
            5 => (4, 2),
            _ => (0, 0)
        };

    public static int Art40MaxEthiopianAgencies(PartnerCapacityTier tier) =>
        tier switch
        {
            PartnerCapacityTier.Low => 2,
            PartnerCapacityTier.Medium => 4,
            PartnerCapacityTier.High => 8,
            _ => 0
        };

    public static string Describe(int agencyLevel)
    {
        var (perCountry, maxCountries) = GetCaps(agencyLevel);
        var countries = maxCountries is null
            ? "unlimited destination countries"
            : $"up to {maxCountries} destination countries";
        return $"Level {agencyLevel}: ≤{perCountry} foreign partners per country, {countries}";
    }

    public static bool IsValidLevel(int level) => level is >= MinLevel and <= MaxLevel;

    public static bool CountriesWithinLimit(int agencyLevel, int licensedCountryCount)
    {
        var (_, maxCountries) = GetCaps(agencyLevel);
        if (maxCountries is null) return true;
        return licensedCountryCount <= maxCountries.Value;
    }
}
