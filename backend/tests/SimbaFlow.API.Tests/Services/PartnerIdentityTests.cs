using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// When two partner agencies are the same agency.
///
/// The catalog is shared across the platform, so the same foreign recruiter is entered by every
/// agency in turn and the differences are typing, not substance.
/// </summary>
public class PartnerIdentityTests
{
    [Theory]
    [InlineData("Example Recruitment", "Example Recruitment")]
    [InlineData("Example Recruitment", "EXAMPLE RECRUITMENT")]
    [InlineData("Example Recruitment", "  Example Recruitment  ")]
    [InlineData("Example  Recruitment", "Example Recruitment")]
    [InlineData("Example\tRecruitment", "Example Recruitment")]
    public void TheSameNameTypedDifferentlyIsTheSameName(string one, string other)
    {
        PartnerIdentity.SameAs(one, "Al Malaz, Riyadh", other, "al malaz, riyadh")
            .Should().BeTrue();
    }

    [Fact]
    public void TwoBranchesAtDifferentAddressesAreTwoPartners()
    {
        // One recruiter, two offices, two agreements — and candidates are placed through a
        // specific one of them.
        PartnerIdentity.SameAs(
            "Example Recruitment", "Al Malaz, Riyadh",
            "Example Recruitment", "Al Khobar")
            .Should().BeFalse();
    }

    [Fact]
    public void DifferentNamesAtOneAddressAreTwoPartners()
    {
        // Recruiters share buildings.
        PartnerIdentity.SameAs(
            "Example Recruitment", "Al Malaz, Riyadh",
            "Second Recruitment", "Al Malaz, Riyadh")
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "   ")]
    public void NoAddressOnEitherStillCountsAsTheSameAddress(string? one, string? other)
    {
        PartnerIdentity.SameAs("Example Recruitment", one, "Example Recruitment", other)
            .Should().BeTrue();
    }

    [Fact]
    public void AnAddressOnOnlyOneOfThemIsNotAMatch()
    {
        // Whoever typed the address knew something the other entry does not say. Treating the two
        // as the same would block the fuller record from being added at all.
        PartnerIdentity.SameAs("Example Recruitment", null, "Example Recruitment", "Al Malaz")
            .Should().BeFalse();
    }

    [Fact]
    public void PunctuationIsNotIgnored()
    {
        // Deliberately: dropping "Est." or "Co." would start merging agencies that are genuinely
        // different, and wrongly refusing a real partner is worse than one duplicate row.
        PartnerIdentity.SameAs("Example Est.", "Riyadh", "Example Est", "Riyadh")
            .Should().BeFalse();
    }
}
