using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using NSubstitute;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Every CV layout is a one-page form.
///
/// These are printed and handed to an embassy or a partner, who expect a single sheet. A box sized
/// by hand — and several of them are, because QuestPDF has no way to make one half of a row take
/// its height from the other half — is one careless number away from pushing the form onto a second
/// page, where the overflow is a mostly-empty sheet nobody notices until it is in an envelope.
/// </summary>
public class CvPageCountTests
{
    private static CvGenerationService Service()
    {
        var branding = Substitute.For<IDocumentBrandingService>();
        branding.GetHeaderLogoAsync(Arg.Any<Candidate>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));
        return new CvGenerationService(branding);
    }

    /// <summary>Counts page objects in the PDF — /Type /Pages is the tree root, not a page.</summary>
    private static int PageCount(byte[] pdf) =>
        Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page(?![s])").Count;

    [Fact]
    public async Task EveryLayoutFitsOnOneSheet()
    {
        var service = Service();

        foreach (var (value, name, _) in CvTemplates.All)
        {
            var pdf = await service.GeneratePreviewAsync(value);
            PageCount(pdf).Should().Be(1, $"the {name} CV is a single-sheet form");
        }
    }
}
