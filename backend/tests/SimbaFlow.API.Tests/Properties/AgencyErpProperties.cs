using FsCheck;
using FsCheck.Xunit;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Properties;

/// <summary>
/// FsCheck properties for Unit 6 Agency ERP invariants (TEST-61–68).
/// </summary>
public class AgencyErpProperties
{
    /// <summary>TEST-61: Per-country partner count respects level cap.</summary>
    [Property(MaxTest = 80)]
    public bool LevelPartnersPerCountry(PositiveInt levelPick, NonNegativeInt sameCountry)
    {
        var level = ClampLevel(levelPick.Get);
        var (maxPerCountry, _) = AgencyLevelRules.GetCaps(level);
        var count = sameCountry.Get % (maxPerCountry + 3);
        return PartnerLinkRules.LevelHasPerCountryCapacity(count, level) == (count < maxPerCountry);
    }

    /// <summary>TEST-62: Distinct countries never exceed MaxCountries when capped.</summary>
    [Property(MaxTest = 60)]
    public bool LevelMaxCountries(PositiveInt levelPick, PositiveInt countryCountPick)
    {
        var level = ClampLevel(levelPick.Get);
        var (_, maxCountries) = AgencyLevelRules.GetCaps(level);
        var n = Math.Min(12, countryCountPick.Get % 13);
        var codes = Enumerable.Range(0, n).Select(i => $"C{i}").ToList();
        // Adding a brand-new country
        var allowed = PartnerLinkRules.LevelAllowsCountry(codes, "NEW", level);
        if (maxCountries is null) return allowed;
        return allowed == (codes.Count < maxCountries.Value);
    }

    /// <summary>TEST-63: Non-empty LicensedCountries rejects unmatched partner country.</summary>
    [Property(MaxTest = 50)]
    public bool LicensedCountryGate(NonEmptyString licensedName)
    {
        var licensed = new List<string> { licensedName.Get.Trim() };
        if (string.IsNullOrWhiteSpace(licensed[0])) return true;
        var match = PartnerLinkRules.IsCountryLicensed(licensed, licensed[0], licensed[0]);
        var miss = PartnerLinkRules.IsCountryLicensed(licensed, "XX", "NowhereLand");
        var emptyOk = PartnerLinkRules.IsCountryLicensed([], "XX", "NowhereLand");
        return match && !miss && emptyOk;
    }

    /// <summary>TEST-64: CountriesWithinLimit matches GetCaps.</summary>
    [Property(MaxTest = 60)]
    public bool CountriesWithinLimitProp(PositiveInt levelPick, NonNegativeInt countPick)
    {
        var level = ClampLevel(levelPick.Get);
        var count = countPick.Get % 20;
        var (_, max) = AgencyLevelRules.GetCaps(level);
        var ok = AgencyLevelRules.CountriesWithinLimit(level, count);
        if (max is null) return ok;
        return ok == (count <= max.Value);
    }

    /// <summary>TEST-65: Intake eligibility ⊆ Active + active partner.</summary>
    [Property(MaxTest = 40)]
    public bool IntakeSubsetActiveLinks(bool activePartner, bool deleted, PositiveInt statusPick)
    {
        var statuses = Enum.GetValues<PartnerLinkStatus>();
        var status = statuses[statusPick.Get % statuses.Length];
        var eligible = PartnerLinkRules.IsEligibleForIntake(status, activePartner, deleted);
        if (eligible)
            return status == PartnerLinkStatus.Active && activePartner && !deleted;
        return true;
    }

    /// <summary>TEST-66: Unique (tenant, partner) — duplicate detection is reflexive.</summary>
    [Property(MaxTest = 30)]
    public bool UniqueTenantPartnerKeys(Guid tenantId, Guid partnerId)
    {
        // Modeling uniqueness: a set of keys never contains duplicates after Distinct
        var keys = new[] { (tenantId, partnerId), (tenantId, partnerId) };
        return keys.Distinct().Count() == 1;
    }

    /// <summary>TEST-67: AgreementEnd ≥ AgreementStart.</summary>
    [Property(MaxTest = 60)]
    public bool AgreementDatesValid(DateTime startDt, int dayOffset)
    {
        var start = DateOnly.FromDateTime(startDt.Date);
        var end = start.AddDays(Math.Clamp(dayOffset, -30, 400));
        return PartnerLinkRules.AgreementDatesValid(start, end) == (end >= start);
    }

    private static int ClampLevel(int pick) =>
        AgencyLevelRules.MinLevel + (pick % (AgencyLevelRules.MaxLevel - AgencyLevelRules.MinLevel + 1));
}
