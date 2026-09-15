using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Partners;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Services.Documents;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Whose letterhead a candidate's documents are printed on.
///
/// The paperwork goes to the embassy under the partner's name when there is one, so the partner's
/// logo wins over the agency's — but only when they actually have one on file.
/// </summary>
public class DocumentBrandingServiceTests : IDisposable
{
    private readonly PlatformDbContext _context;
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _partnerId = Guid.NewGuid();

    private static readonly byte[] AgencyLogo = Encoding.UTF8.GetBytes("agency-logo");
    private static readonly byte[] PartnerLogo = Encoding.UTF8.GetBytes("partner-logo");
    private static readonly byte[] AgencyLetterhead = Encoding.UTF8.GetBytes("agency-letterhead");
    private static readonly byte[] PartnerLetterhead = Encoding.UTF8.GetBytes("partner-letterhead");

    public DocumentBrandingServiceTests()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new PlatformDbContext(options, _currentUser);
        _currentUser.TenantId.Returns(_tenantId);

        _storage.DownloadAsync("agency/logo.png", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream?>(new MemoryStream(AgencyLogo)));
        _storage.DownloadAsync("partner/logo.png", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream?>(new MemoryStream(PartnerLogo)));
        _storage.DownloadAsync("agency/letterhead.png", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream?>(new MemoryStream(AgencyLetterhead)));
        _storage.DownloadAsync("partner/letterhead.png", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream?>(new MemoryStream(PartnerLetterhead)));
    }

    private DocumentBrandingService Service() => new(
        _context, _storage, _currentUser, Substitute.For<ILogger<DocumentBrandingService>>());

    private void GivenAgencyLogo(string? path, string? letterhead = null)
    {
        _context.Tenants.Add(new TenantInfo
        {
            Id = _tenantId, Name = "Test Agency", SchemaName = "tenant_test",
            LogoPath = path, LetterheadPath = letterhead,
        });
        _context.SaveChanges();
    }

    private void GivenPartner(string? logoPath, string? letterhead = null)
    {
        _context.PartnerAgencies.Add(new PartnerAgency
        {
            Id = _partnerId, Name = "Partner", CountryCode = "SA", CountryName = "Saudi Arabia",
            LogoPath = logoPath, LetterheadPath = letterhead,
        });
        _context.SaveChanges();
    }

    private static Candidate CandidateWith(Guid? partnerAgencyId) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "Candidate",
        PartnerAgencyId = partnerAgencyId,
    };

    [Fact]
    public async Task ThePartnersLetterheadIsUsedWhenTheCandidateIsPlacedWithOne()
    {
        GivenAgencyLogo("agency/logo.png");
        GivenPartner("partner/logo.png");

        var logo = await Service().GetHeaderLogoAsync(CandidateWith(_partnerId));

        logo.Should().Equal(PartnerLogo);
    }

    [Fact]
    public async Task TheAgencysOwnLetterheadIsUsedWhenThereIsNoPartner()
    {
        GivenAgencyLogo("agency/logo.png");

        var logo = await Service().GetHeaderLogoAsync(CandidateWith(null));

        logo.Should().Equal(AgencyLogo);
    }

    [Fact]
    public async Task TheAgencysLetterheadStandsInWhenThePartnerHasNotUploadedOne()
    {
        GivenAgencyLogo("agency/logo.png");
        GivenPartner(logoPath: null);

        var logo = await Service().GetHeaderLogoAsync(CandidateWith(_partnerId));

        logo.Should().Equal(AgencyLogo,
            "a partner without a letterhead should not leave the page bare");
    }

    [Fact]
    public async Task NothingIsReturnedWhenNeitherHasALetterhead()
    {
        GivenAgencyLogo(null);
        GivenPartner(null);

        var logo = await Service().GetHeaderLogoAsync(CandidateWith(_partnerId));

        logo.Should().BeNull("the document falls back to the printed agency name");
    }

    [Fact]
    public async Task AMissingFileDoesNotStopTheDocumentBeingProduced()
    {
        GivenAgencyLogo("agency/deleted-by-hand.png");
        _storage.DownloadAsync("agency/deleted-by-hand.png", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream?>(null));

        var logo = await Service().GetHeaderLogoAsync(CandidateWith(null));

        logo.Should().BeNull();
    }

    [Fact]
    public async Task TheLetterheadIsPreferredOverTheLogo()
    {
        GivenAgencyLogo("agency/logo.png", "agency/letterhead.png");

        var logo = await Service().GetHeaderLogoAsync(CandidateWith(null));

        logo.Should().Equal(AgencyLetterhead,
            "the letterhead is the banner meant for the top of a printed page");
    }

    [Fact]
    public async Task ThePartnersLetterheadBeatsTheAgencysLetterhead()
    {
        GivenAgencyLogo("agency/logo.png", "agency/letterhead.png");
        GivenPartner("partner/logo.png", "partner/letterhead.png");

        var logo = await Service().GetHeaderLogoAsync(CandidateWith(_partnerId));

        logo.Should().Equal(PartnerLetterhead);
    }

    [Fact]
    public async Task ThePartnersLogoStandsInBeforeFallingBackToTheAgency()
    {
        GivenAgencyLogo("agency/logo.png", "agency/letterhead.png");
        GivenPartner("partner/logo.png", letterhead: null);

        var logo = await Service().GetHeaderLogoAsync(CandidateWith(_partnerId));

        logo.Should().Equal(PartnerLogo,
            "a partner that uploaded only a mark is still the right name on the paperwork");
    }

    [Fact]
    public async Task TheAgencysChosenCvLayoutIsUsed()
    {
        _context.Tenants.Add(new TenantInfo
        {
            Id = _tenantId, Name = "Test Agency", SchemaName = "tenant_test",
            Settings = new TenantSettings { Documents = new DocumentSettings { CvTemplate = "profile" } },
        });
        _context.SaveChanges();

        (await Service().GetCvTemplateAsync()).Should().Be("profile");
    }

    [Fact]
    public async Task AnUnknownLayoutFallsBackRatherThanFailing()
    {
        _context.Tenants.Add(new TenantInfo
        {
            Id = _tenantId, Name = "Test Agency", SchemaName = "tenant_test",
            Settings = new TenantSettings { Documents = new DocumentSettings { CvTemplate = "retired-layout" } },
        });
        _context.SaveChanges();

        (await Service().GetCvTemplateAsync()).Should().Be(CvTemplates.Default,
            "a layout that has since been removed must not stop a CV being produced");
    }

    [Fact]
    public async Task AnAgencyThatHasNotChosenGetsTheDefaultLayout()
    {
        GivenAgencyLogo(null);

        (await Service().GetCvTemplateAsync()).Should().Be(CvTemplates.Default);
    }

    public void Dispose() => _context.Dispose();
}
