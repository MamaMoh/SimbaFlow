using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// A partner belongs to the agency that registered it.
///
/// The catalog was shared across the platform on the reasoning that the same Saudi recruiter is
/// the same company whoever is looking at it. True of the company, false of the record: who an
/// agency works with is its own commercial business, and a shared list put it in front of every
/// competitor on the platform.
///
/// Inside one agency's list the name alone settles a duplicate. The address used to be part of
/// it because two agencies entering the same recruiter's two branches both had a claim on the
/// name; an agency's own list has no such excuse.
/// </summary>
public class PartnerOwnershipTests
{
    [Theory]
    [InlineData("Gulf Manpower", "Gulf Manpower", true)]
    [InlineData("gulf manpower", "GULF MANPOWER", true)]
    [InlineData("  Gulf   Manpower ", "Gulf Manpower", true)]
    [InlineData("Gulf Manpower", "Gulf Manpower Est.", false)]
    public void TheSameNameIsTheSamePartnerHoweverItWasTyped(string a, string b, bool same) =>
        PartnerIdentity.SameNameAs(a, b).Should().Be(same);

    [Fact]
    public void TheAddressNoLongerTellsTwoOfficesApartWithinOneAgency()
    {
        // The old rule: same name, different address, two partners.
        PartnerIdentity.SameAs("Gulf Manpower", "Riyadh", "Gulf Manpower", "Jeddah")
            .Should().BeFalse();

        // The rule for one agency's own list: the second one is the first one typed again.
        PartnerIdentity.SameNameAs("Gulf Manpower", "Gulf Manpower").Should().BeTrue();
    }
}
