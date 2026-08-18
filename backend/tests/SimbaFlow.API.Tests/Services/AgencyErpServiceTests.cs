using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Partners;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Example-based tests for Unit 6 Agency ERP (TEST-60–68).
/// </summary>
public class AgencyErpServiceTests
{
    private static PlatformDbContext CreatePlatformDb()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"agency_erp_{Guid.NewGuid()}")
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        return new PlatformDbContext(options, currentUser);
    }


    [Fact]
    public void LevelPerCountry_RejectsAtCap_TEST61()
    {
        // Level 5: max 4 partners per country
        PartnerLinkRules.LevelHasPerCountryCapacity(3, 5).Should().BeTrue();
        PartnerLinkRules.LevelHasPerCountryCapacity(4, 5).Should().BeFalse();
        AgencyLevelRules.GetCaps(5).MaxPartnersPerCountry.Should().Be(4);
    }

    [Fact]
    public void LevelMaxCountries_RejectsNewCountryAtCap_TEST62()
    {
        // Level 5: max 2 countries
        PartnerLinkRules.LevelAllowsCountry(["SA"], "SA", 5).Should().BeTrue(); // same country OK
        PartnerLinkRules.LevelAllowsCountry(["SA"], "AE", 5).Should().BeTrue(); // second country OK
        PartnerLinkRules.LevelAllowsCountry(["SA", "AE"], "KW", 5).Should().BeFalse();
        PartnerLinkRules.LevelAllowsCountry(["SA", "AE"], "AE", 5).Should().BeTrue();
        // Level 1: unlimited countries
        PartnerLinkRules.LevelAllowsCountry(["SA", "AE", "KW", "QA"], "OM", 1).Should().BeTrue();
    }

    [Fact]
    public void LicensedCountryGate_EmptyListAllowsAny_TEST63()
    {
        PartnerLinkRules.IsCountryLicensed([], "SA", "Saudi Arabia").Should().BeTrue();
        PartnerLinkRules.IsCountryLicensed(null, "KW", "Kuwait").Should().BeTrue();
        PartnerLinkRules.IsCountryLicensed(["Saudi Arabia"], "SA", "Saudi Arabia").Should().BeTrue();
        PartnerLinkRules.IsCountryLicensed(["SA"], "SA", "Saudi Arabia").Should().BeTrue();
        PartnerLinkRules.IsCountryLicensed(["Saudi Arabia"], "KW", "Kuwait").Should().BeFalse();
    }

    [Fact]
    public void CountriesWithinLimit_OnLicenseSave_TEST64()
    {
        AgencyLevelRules.CountriesWithinLimit(5, 2).Should().BeTrue();
        AgencyLevelRules.CountriesWithinLimit(5, 3).Should().BeFalse();
        AgencyLevelRules.CountriesWithinLimit(1, 20).Should().BeTrue(); // unlimited
        AgencyLevelRules.CountriesWithinLimit(4, 4).Should().BeTrue();
        AgencyLevelRules.CountriesWithinLimit(4, 5).Should().BeFalse();
    }

    [Fact]
    public void IntakeEligible_OnlyActivePartnerLinks_TEST65()
    {
        PartnerLinkRules.IsEligibleForIntake(PartnerLinkStatus.Active, true, false).Should().BeTrue();
        PartnerLinkRules.IsEligibleForIntake(PartnerLinkStatus.Suspended, true, false).Should().BeFalse();
        PartnerLinkRules.IsEligibleForIntake(PartnerLinkStatus.Active, false, false).Should().BeFalse();
        PartnerLinkRules.IsEligibleForIntake(PartnerLinkStatus.Active, true, true).Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateTenantPartnerLink_Detected_TEST66()
    {
        await using var db = CreatePlatformDb();
        var tenantId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();

        db.PartnerAgencies.Add(new PartnerAgency
        {
            Id = partnerId,
            Name = "Al Nour",
            CountryCode = "AE",
            CountryName = "United Arab Emirates",
            IsActive = true
        });
        db.PartnerLinks.Add(new PartnerLink
        {
            TenantId = tenantId,
            PartnerAgencyId = partnerId,
            AgreementStart = new DateOnly(2026, 1, 1),
            AgreementEnd = new DateOnly(2028, 1, 1),
            Status = PartnerLinkStatus.Active
        });
        await db.SaveChangesAsync();

        var exists = await db.PartnerLinks.AnyAsync(l =>
            l.TenantId == tenantId && l.PartnerAgencyId == partnerId && !l.IsDeleted);
        exists.Should().BeTrue();
    }

    [Fact]
    public void AgreementDates_EndMustBeOnOrAfterStart_TEST67()
    {
        var start = new DateOnly(2026, 1, 1);
        PartnerLinkRules.AgreementDatesValid(start, start).Should().BeTrue();
        PartnerLinkRules.AgreementDatesValid(start, start.AddDays(1)).Should().BeTrue();
        PartnerLinkRules.AgreementDatesValid(start, start.AddDays(-1)).Should().BeFalse();
    }
}
