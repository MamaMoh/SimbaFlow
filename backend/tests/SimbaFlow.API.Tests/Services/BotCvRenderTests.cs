using FluentAssertions;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// A CV the Telegram bot sends is the same document the web application prints.
///
/// There is no signed-in user behind a Telegram message, so the renderer cannot read the agency
/// off the request the way it does in the browser. It read nothing, fell back to the default
/// layout and no letterhead, and the bot was never given the photographs either — so the same
/// candidate came out as two different documents depending on where you asked for them.
/// </summary>
public class BotCvRenderTests
{
    private sealed class StubBranding : IDocumentBrandingService
    {
        public Guid? AskedFor { get; private set; }
        public bool AskedWithoutTenant { get; private set; }

        public Task<byte[]?> GetHeaderLogoAsync(Candidate c, CancellationToken ct = default)
        {
            AskedWithoutTenant = true;
            return Task.FromResult<byte[]?>(null);
        }

        public Task<byte[]?> GetHeaderLogoAsync(Candidate c, Guid? tenantId, CancellationToken ct = default)
        {
            AskedFor = tenantId;
            return Task.FromResult<byte[]?>(null);
        }

        // Deliberately a layout nobody asked for: if the renderer reaches for this instead of
        // the template it was handed, the test should notice.
        public Task<string> GetCvTemplateAsync(CancellationToken ct = default) =>
            Task.FromResult(CvTemplates.Layout7);

        public Task<AgencyIdentity> GetAgencyIdentityAsync(CancellationToken ct = default) =>
            Task.FromResult(new AgencyIdentity("SAMPLE AGENCY PLC", null, null, null));
    }

    private static Candidate Sample() => new()
    {
        FirstName = "SAMPLE",
        LastName = "CANDIDATE",
        PassportNumber = "E00000001",
        Gender = Domain.Enums.Gender.Female,
        CountryOfTravel = "Saudi Arabia",
    };

    [Fact]
    public async Task TheAgencysOwnLayoutIsUsed()
    {
        var branding = new StubBranding();
        var service = new CvGenerationService(branding);

        var chosen = await service.GenerateForTenantAsync(
            Sample(), Guid.NewGuid(), CvTemplates.Layout1);
        var other = await service.GenerateForTenantAsync(
            Sample(), Guid.NewGuid(), CvTemplates.Layout5);

        chosen.Should().NotBeEmpty();
        // Two different forms produce two different documents; one layout for every agency
        // would produce the same bytes twice.
        chosen.Length.Should().NotBe(other.Length);
        branding.AskedWithoutTenant.Should().BeFalse();
    }

    [Fact]
    public async Task TheLetterheadIsLookedUpForTheAgencyThatWasNamed()
    {
        var branding = new StubBranding();
        var tenantId = Guid.NewGuid();

        await new CvGenerationService(branding)
            .GenerateForTenantAsync(Sample(), tenantId, CvTemplates.Layout3);

        branding.AskedFor.Should().Be(tenantId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a-layout-we-retired")]
    public async Task AnAgencyThatHasNotChosenStillGetsADocument(string? template)
    {
        var pdf = await new CvGenerationService(new StubBranding())
            .GenerateForTenantAsync(Sample(), Guid.NewGuid(), template);

        pdf.Should().NotBeEmpty();
    }
}
