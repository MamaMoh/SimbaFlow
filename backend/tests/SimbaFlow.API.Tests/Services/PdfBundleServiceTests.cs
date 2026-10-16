using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Infrastructure.Services.Documents;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The merge itself, against the real qpdf engine.
///
/// The handler's tests stand a substitute in for this service, so without these the riskiest part of
/// a batch download — a native library, a temp directory and files uploaded years ago by a browser
/// that lied about their type — would ship unexercised. What is pinned here is the promise the desk
/// depends on: one bad file costs its own page, never the whole packet.
/// </summary>
public class PdfBundleServiceTests
{
    private readonly PdfBundleService _service = new();

    /// <summary>Counts page objects in the PDF — /Type /Pages is the tree root, not a page.</summary>
    private static int PageCount(byte[] pdf) =>
        Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page(?![s])").Count;

    /// <summary>A real PDF of the requested length, rendered the way the CVs are.</summary>
    private static byte[] Pdf(int pages) =>
        Document.Create(container =>
        {
            for (var i = 1; i <= pages; i++)
            {
                var label = $"page {i}";
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.Content().Text(label);
                });
            }
        }).GeneratePdf();

    private static byte[] Png()
    {
        using var image = new Image<Rgba32>(120, 160);
        image.Mutate(c => c.BackgroundColor(Color.CadetBlue));
        using var buffer = new MemoryStream();
        image.SaveAsPng(buffer);
        return buffer.ToArray();
    }

    private static byte[] Jpeg()
    {
        using var image = new Image<Rgba32>(160, 120);
        image.Mutate(c => c.BackgroundColor(Color.Coral));
        using var buffer = new MemoryStream();
        image.SaveAsJpeg(buffer);
        return buffer.ToArray();
    }

    [Fact]
    public async Task EveryPageOfEveryDocumentSurvivesTheMerge()
    {
        var result = await _service.MergeAsync([
            PdfBundleItem.Document("passport", Pdf(1)),
            PdfBundleItem.Document("contract", Pdf(3)),
        ]);

        result.Should().NotBeNull();
        PageCount(result!).Should().Be(4, "a merged packet is as long as the documents that went in");
    }

    [Fact]
    public async Task ASinglePdfComesBackWhole()
    {
        // Nothing to merge, but it still has to be a readable PDF rather than the raw bytes.
        var result = await _service.MergeAsync([PdfBundleItem.Document("cv", Pdf(2))]);

        result.Should().NotBeNull();
        Encoding.Latin1.GetString(result!, 0, 4).Should().Be("%PDF");
        PageCount(result!).Should().Be(2);
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    public async Task APhotoBecomesAPageRatherThanBeingLeftOut(string format)
    {
        // A passport scan is a photograph on most desks, and the packet is incomplete without it.
        var photo = format == "png" ? Png() : Jpeg();

        var result = await _service.MergeAsync([
            PdfBundleItem.Document("SEADA MEKONNEN · Passport", photo),
            PdfBundleItem.Document("SEADA MEKONNEN · CV", Pdf(1)),
        ]);

        result.Should().NotBeNull();
        PageCount(result!).Should().Be(2);
    }

    [Fact]
    public async Task AFileThatCannotBecomeAPageIsSkipped_NotGuessedAt()
    {
        // A .docx begins PK\x03\x04. There is nothing here that could render it.
        var docx = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00 };

        var result = await _service.MergeAsync([
            PdfBundleItem.Document("contract.docx", docx),
            PdfBundleItem.Document("cv", Pdf(1)),
        ]);

        result.Should().NotBeNull();
        PageCount(result!).Should().Be(1, "only the CV could be paged");
    }

    [Fact]
    public async Task OneUnreadableFileDoesNotSinkTheWholeBatch()
    {
        // Claims to be a PDF and is not: truncated uploads and half-written scans both look like
        // this, and qpdf refuses the entire batch when one input is bad.
        var broken = Encoding.Latin1.GetBytes("%PDF-1.7\nnot actually a pdf at all\n");

        var result = await _service.MergeAsync([
            PdfBundleItem.Document("broken", broken),
            PdfBundleItem.Document("passport", Pdf(1)),
            PdfBundleItem.Document("cv", Pdf(2)),
        ]);

        result.Should().NotBeNull("the other nineteen people still need their paperwork");
        PageCount(result!).Should().Be(3);
    }

    [Fact]
    public async Task ADividerAnnouncesTheDocumentsBehindIt()
    {
        var result = await _service.MergeAsync([
            PdfBundleItem.Divider("SEADA MEKONNEN"),
            PdfBundleItem.Document("passport", Pdf(1)),
        ]);

        result.Should().NotBeNull();
        PageCount(result!).Should().Be(2, "the divider is a page of its own");
    }

    [Fact]
    public async Task ADividerWithNothingBehindItIsDropped()
    {
        // Every one of this candidate's files was unusable. A page announcing paperwork that is not
        // there is worse than no page at all.
        var docx = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

        var result = await _service.MergeAsync([
            PdfBundleItem.Divider("HANAN ABDI"),
            PdfBundleItem.Document("contract.docx", docx),
            PdfBundleItem.Divider("SEADA MEKONNEN"),
            PdfBundleItem.Document("passport", Pdf(1)),
        ]);

        result.Should().NotBeNull();
        PageCount(result!).Should().Be(2, "one divider and one passport, the empty name dropped");
    }

    [Fact]
    public async Task NothingToPageIsSaidPlainly_NotHandedBackAsAnEmptyFile()
    {
        var docx = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

        var result = await _service.MergeAsync([
            PdfBundleItem.Divider("SEADA MEKONNEN"),
            PdfBundleItem.Document("contract.docx", docx),
        ]);

        // The handler turns this into a 404 rather than serving a zero-page PDF.
        result.Should().BeNull();
    }

    [Fact]
    public async Task NoStagingDirectoryIsLeftBehind()
    {
        var before = Directory.GetDirectories(Path.GetTempPath(), "simbaflow-bundle-*").Length;

        await _service.MergeAsync([
            PdfBundleItem.Document("passport", Pdf(1)),
            PdfBundleItem.Document("cv", Pdf(1)),
        ]);
        // Also on the way out of a failed run.
        await _service.MergeAsync([PdfBundleItem.Divider("nobody")]);

        Directory.GetDirectories(Path.GetTempPath(), "simbaflow-bundle-*").Length
            .Should().Be(before);
    }

    // ──── Photos and passports at their own size ────
    //
    // A photo used to be stretched to fill the A4 page, so a 600-pixel phone picture printed at
    // about 80 dpi and came out blurred.

    [Fact]
    public void ImageSizeIsReadFromThePngAndJpegHeaders()
    {
        PdfBundleService.PixelSize(Png()).Should().Be((120, 160));
        PdfBundleService.PixelSize(Jpeg()).Should().Be((160, 120));
        PdfBundleService.PixelSize([0x50, 0x4B, 0x03, 0x04]).Should().Be((0, 0));
    }

    [Fact]
    public void ASmallPhotoPrintsAtItsOwnResolution_NotStretched()
    {
        // 600 × 800 pixels at 150 dpi is 288 × 384 points: well inside the snapshot box, so left alone.
        var (width, height) = PdfBundleService.PrintSize(600, 800, 400, 460);

        width.Should().BeApproximately(288, 0.01f);
        height.Should().BeApproximately(384, 0.01f);
    }

    [Fact]
    public void ALargePhotoIsHeldToTheBox_KeepingItsShape()
    {
        // A 12-megapixel phone shot of a passport, landscape.
        var (width, height) = PdfBundleService.PrintSize(4000, 3000, 400, 460);

        width.Should().BeApproximately(400, 0.01f);
        height.Should().BeApproximately(300, 0.01f);
    }

    [Fact]
    public void ThePortraitPrintsAFifthLargerThanASnapshot()
    {
        // The same 600 × 800 phone photo: 288 × 384 points as a snapshot, about a fifth larger as
        // the portrait, because it may print at 125 dpi inside its bigger box.
        var (width, height) = PdfBundleService.PrintSize(600, 800, 440, 560, 125);

        width.Should().BeApproximately(345.6f, 0.01f);
        height.Should().BeApproximately(460.8f, 0.01f);
    }

    [Fact]
    public void APortraitIsAlsoASnapshot()
    {
        // Held to a box like any photo, never stretched across the page.
        var item = PdfBundleItem.Document("SEADA MEKONNEN · Photo", Png(), isPortrait: true);

        item.IsPortrait.Should().BeTrue();
        item.IsSnapshot.Should().BeTrue();
    }

    [Fact]
    public async Task ABigPassportPhotoStillMakesOnePage()
    {
        using var image = new Image<Rgba32>(4000, 3000);
        image.Mutate(c => c.BackgroundColor(Color.Khaki));
        using var buffer = new MemoryStream();
        image.SaveAsJpeg(buffer);

        var result = await _service.MergeAsync([
            PdfBundleItem.Document("SEADA MEKONNEN · Passport", buffer.ToArray(), isSnapshot: true),
            PdfBundleItem.Document("SEADA MEKONNEN · Photo", Png(), isSnapshot: true),
        ]);

        result.Should().NotBeNull();
        PageCount(result!).Should().Be(2);
    }
}
