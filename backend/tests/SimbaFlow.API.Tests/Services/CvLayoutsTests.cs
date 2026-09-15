using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Every layout an agency can pick has to actually exist.
///
/// The choice is stored as a string, so a name in the picker that no renderer handles would fall
/// through to the default and quietly print the wrong form — the kind of mistake nobody notices
/// until a partner rejects the paperwork.
/// </summary>
public class CvLayoutsTests
{
    [Fact]
    public void EveryOfferedLayoutIsDistinctAndNamed()
    {
        CvTemplates.All.Should().HaveCountGreaterThan(1);
        CvTemplates.All.Select(t => t.Value).Should().OnlyHaveUniqueItems();
        CvTemplates.All.Should().OnlyContain(t =>
            !string.IsNullOrWhiteSpace(t.Name) && !string.IsNullOrWhiteSpace(t.Description));
    }

    [Fact]
    public void TheDefaultIsOneOfTheOfferedLayouts()
    {
        CvTemplates.All.Select(t => t.Value).Should().Contain(CvTemplates.Default);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("a-layout-we-retired")]
    public void AnUnknownLayoutNormalisesToTheDefault(string? stored)
    {
        CvTemplates.Normalise(stored).Should().Be(CvTemplates.Default);
    }

    [Fact]
    public void AKnownLayoutIsKept()
    {
        foreach (var (value, _, _) in CvTemplates.All)
            CvTemplates.Normalise(value).Should().Be(value);
    }
}
