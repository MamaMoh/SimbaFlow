using FluentAssertions;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The CV renders, for a candidate with every field filled in.
///
/// These layouts are a page of nested tables, and the failures they produce are layout failures:
/// a box that runs past the sheet, a column that will not fit beside its neighbour. None of that
/// shows up in a unit test of anything else, and it is only visible on the printed page.
///
/// Set SIMBAFLOW_CV_OUT to a directory and this writes the PDF there, which is how the layout was
/// compared against the form it is meant to match.
/// </summary>
public class CvRenderTests
{
    private sealed class StubBranding(string template) : IDocumentBrandingService
    {
        public Task<byte[]?> GetHeaderLogoAsync(Candidate c, CancellationToken ct = default) =>
            Task.FromResult<byte[]?>(null);

        public Task<string> GetCvTemplateAsync(CancellationToken ct = default) =>
            Task.FromResult(template);
    }

    private static Candidate SampleCandidate() => new()
    {
        FirstName = "ZINET",
        MiddleName = "YIBRE",
        LastName = "ABEBAW",
        PassportNumber = "E00201748",
        PassportIssueDate = new DateOnly(2026, 3, 6),
        PassportExpiryDate = new DateOnly(2031, 3, 5),
        PassportPlaceOfIssue = "ETHIOPIA",
        DateOfBirth = new DateOnly(2005, 6, 18),
        PlaceOfBirth = "SOUTH WOLLO",
        Nationality = "Ethiopia",
        Religion = "Muslim",
        MaritalStatus = "Single",
        NumberOfChildren = 0,
        PhoneNumber = "+251926646949",
        Occupation = "HOUSE MAID",
        MonthlySalary = "1000",
        ContractPeriod = "2 Years",
        CountryOfTravel = "Saudi Arabia",
        Qualification = "SECONDARY LEVEL",
        ApplicationNo = "APP-20261007-0001",
        SkillCleaning = true,
        SkillWashing = true,
        SkillBabysitting = true,
        CookingLevel = "None",
    };

    [Theory]
    [InlineData("layout1")]
    [InlineData("layout2")]
    [InlineData("layout3")]
    [InlineData("layout4")]
    [InlineData("layout5")]
    [InlineData("layout7")]
    public async Task EveryLayoutRendersAPdf(string template)
    {
        var service = new CvGenerationService(new StubBranding(template));

        var pdf = await service.GenerateAsync(SampleCandidate());

        pdf.Should().NotBeNullOrEmpty();
        // "%PDF" — a renderer that fails quietly is worse than one that throws.
        pdf.Take(4).Should().Equal((byte)'%', (byte)'P', (byte)'D', (byte)'F');

        var outDir = Environment.GetEnvironmentVariable("SIMBAFLOW_CV_OUT");
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
            await File.WriteAllBytesAsync(Path.Combine(outDir, $"{template}.pdf"), pdf);
        }
    }
}
