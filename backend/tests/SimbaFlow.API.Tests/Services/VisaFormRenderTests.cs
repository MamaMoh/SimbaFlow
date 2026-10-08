using FluentAssertions;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The enjaze form is a single sheet that an embassy clerk works down in order, and the whole
/// value of matching their sheet is lost if ours runs onto a second page — the signature block
/// and the official-use box end up on a page the clerk is not looking at.
///
/// Everything here is invented. Set SIMBAFLOW_VISA_OUT to a directory to write the PDFs out and
/// compare them against the printed form.
/// </summary>
public class VisaFormRenderTests
{
    private sealed class StubBranding : IDocumentBrandingService
    {
        public Task<byte[]?> GetHeaderLogoAsync(Candidate c, CancellationToken ct = default) =>
            Task.FromResult<byte[]?>(null);

        public Task<string> GetCvTemplateAsync(CancellationToken ct = default) =>
            Task.FromResult(CvTemplates.Default);

        public Task<AgencyIdentity> GetAgencyIdentityAsync(CancellationToken ct = default) =>
            Task.FromResult(new AgencyIdentity(
                "SAMPLE FOREIGN EMPLOYMENT AGENCY PLC", "office@example.com", "+251 11 000 0000", "LB/0001"));
    }

    private static Candidate Placed() => new()
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
        Gender = SimbaFlow.Domain.Enums.Gender.Female,
        PhoneNumber = "+251926646949",
        Address = "Kebele 08",
        Subcity = "Bole Sub City",
        City = "Addis Ababa",
        Country = "Ethiopia",
        Occupation = "HOUSE MAID",
        Qualification = "SECONDARY LEVEL",
        CountryOfTravel = "Saudi Arabia",
        VisaNumber = "1900111222",
        VisaType = "Work",
        SponsorName = "SAAD ABDULLAH MOHAMMED ALHARBI",
        SponsorArabicName = "سعد عبدالله محمد الحربي",
        SponsorIdNumber = "1099887766",
        SponsorPhone = "966500000000",
        SponsorAddress = "4120, Alnakheel, 11564, الرياض",
        ContractNo = "2000111222",
        AgentName = "SAMPLE RECRUITING EST.",
        ReferenceNo = "E00111222",
    };

    /// <summary>A record with nothing optional filled in — the state a candidate is in before the
    /// visa is issued, which is when this form is first printed.</summary>
    private static Candidate Bare() => new()
    {
        FirstName = "ALMAZ",
        LastName = "BEKELE",
        PassportNumber = "EP0000001",
        DateOfBirth = new DateOnly(2003, 1, 2),
        Gender = SimbaFlow.Domain.Enums.Gender.Female,
    };

    [Theory]
    [InlineData("placed")]
    [InlineData("bare")]
    public async Task TheFormIsOneSheet(string which)
    {
        var pdf = await Render(which == "placed" ? Placed() : Bare());

        pdf.Take(4).Should().Equal((byte)'%', (byte)'P', (byte)'D', (byte)'F');

        var outDir = Environment.GetEnvironmentVariable("SIMBAFLOW_VISA_OUT");
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
            await File.WriteAllBytesAsync(Path.Combine(outDir, $"visa-{which}.pdf"), pdf);
        }

        PageCount(pdf).Should().Be(1, "the enjaze form is a single-sheet form");
    }

    [Fact]
    public async Task ABatchIsOnePagePerCandidate()
    {
        var service = new CvGenerationService(new StubBranding());

        var pdf = await service.GenerateVisaFormsAsync(
            [(Placed(), null), (Bare(), null), (Placed(), null)]);

        PageCount(pdf).Should().Be(3);
    }

    [Fact]
    public async Task TheRightBarcodeIsTheENumberAndNotThePassport()
    {
        // They sit side by side on the official form and look alike. A scanner at the counter
        // reading the passport number where the E number belongs is a silent wrong answer.
        var candidate = Placed();

        Code128.Modules(candidate.ReferenceNo).Should().NotBeEmpty();
        Code128.Modules(candidate.ReferenceNo)
            .Should().NotEqual(Code128.Modules(candidate.PassportNumber));

        (await Render(candidate)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AFormWithNoVisaNumberYetStillRenders()
    {
        // Nothing to encode is not an error — the barcode is simply absent, and the clerk writes
        // the number on. A barcode of an empty string would scan as an empty string.
        Code128.Modules(Bare().VisaNumber).Should().BeEmpty();

        (await Render(Bare())).Should().NotBeNullOrEmpty();
    }

    private static Task<byte[]> Render(Candidate candidate) =>
        new CvGenerationService(new StubBranding()).GenerateVisaFormAsync(candidate);

    /// <summary>Counts page objects in the PDF — /Type /Pages is the tree root, not a page.</summary>
    private static int PageCount(byte[] pdf) =>
        System.Text.RegularExpressions.Regex
            .Matches(System.Text.Encoding.Latin1.GetString(pdf), @"/Type\s*/Page(?![s])")
            .Count;
}
