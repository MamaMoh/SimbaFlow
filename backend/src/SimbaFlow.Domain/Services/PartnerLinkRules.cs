using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// Pure validation for partner link create (Art. 40, agency level, licensed countries, dates).
/// Used by PartnerModule and Unit 6 PBT/example tests.
/// </summary>
public static class PartnerLinkRules
{

    public static bool LevelHasPerCountryCapacity(int activeSameCountryCount, int agencyLevel)
    {
        var (maxPerCountry, _) = AgencyLevelRules.GetCaps(agencyLevel);
        return activeSameCountryCount < maxPerCountry;
    }

    /// <summary>
    /// When the partner's country is already represented among active links, always allowed
    /// (subject to per-country capacity). New country requires room under MaxCountries.
    /// </summary>
    public static bool LevelAllowsCountry(
        IEnumerable<string> activeCountryCodes,
        string newCountryCode,
        int agencyLevel)
    {
        var (_, maxCountries) = AgencyLevelRules.GetCaps(agencyLevel);
        if (maxCountries is null) return true;

        var set = activeCountryCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (set.Contains(newCountryCode)) return true;
        return set.Count < maxCountries.Value;
    }

    /// <summary>
    /// Empty licensed list = no gate. Otherwise partner code or name must match an entry.
    /// </summary>
    public static bool IsCountryLicensed(
        IReadOnlyList<string>? licensedCountries,
        string partnerCountryCode,
        string partnerCountryName)
    {
        var licensed = licensedCountries ?? [];
        if (licensed.Count == 0) return true;
        return licensed.Any(c =>
            string.Equals(c, partnerCountryCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c, partnerCountryName, StringComparison.OrdinalIgnoreCase));
    }

    public static bool AgreementDatesValid(DateOnly start, DateOnly end) => end >= start;

    /// <summary>
    /// Intake / linkedOnly invariant: only Active links to active, non-deleted partners.
    /// </summary>
    public static bool IsEligibleForIntake(
        PartnerLinkStatus status,
        bool partnerIsActive,
        bool partnerIsDeleted) =>
        status == PartnerLinkStatus.Active && partnerIsActive && !partnerIsDeleted;
}
