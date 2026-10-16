using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Services.Documents;

/// <summary>
/// Merges PDFs and photos into one document, using the qpdf engine QuestPDF already ships.
///
/// Photos become a captioned page of their own rather than being left out, because a passport scan
/// is a photograph on most desks and the packet is incomplete without it. Anything neither PDF nor
/// image — a .docx contract, say — is skipped: there is nothing here that could turn it into a page,
/// and a placeholder saying so would be worse than its absence.
/// </summary>
public class PdfBundleService : IPdfBundleService
{
    static PdfBundleService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]?> MergeAsync(
        IReadOnlyList<PdfBundleItem> items, CancellationToken cancellationToken = default)
    {
        // qpdf works on paths, not streams, so the parts are staged in a directory of their own and
        // that directory is removed whether or not the merge succeeds.
        var workspace = Path.Combine(Path.GetTempPath(), $"simbaflow-bundle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);

        try
        {
            var staged = new List<(bool IsDivider, string Path)>();

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pdf = item.IsDivider ? RenderDivider(item.Title) : ToPdfPages(item);
                if (pdf is null) continue;

                var path = Path.Combine(workspace, $"part-{staged.Count:D4}.pdf");
                await File.WriteAllBytesAsync(path, pdf, cancellationToken);

                // A file that survived upload can still be unreadable now, and qpdf rejects the
                // whole batch when one input is bad. Reading each part on its own first costs a
                // pass and keeps one broken scan from taking the download with it.
                var checked_ = Normalise(path, Path.Combine(workspace, $"ok-{staged.Count:D4}.pdf"));
                if (checked_ is null) continue;

                staged.Add((item.IsDivider, checked_));
            }

            // A divider with nothing behind it announces paperwork that is not there. Walking
            // backwards also clears runs of them, left by candidates whose files were all skipped.
            for (var i = staged.Count - 1; i >= 0; i--)
            {
                if (staged[i].IsDivider && (i == staged.Count - 1 || staged[i + 1].IsDivider))
                    staged.RemoveAt(i);
            }

            if (staged.Count == 0) return null;
            if (staged.Count == 1) return await File.ReadAllBytesAsync(staged[0].Path, cancellationToken);

            var output = Path.Combine(workspace, "bundle.pdf");
            var operation = DocumentOperation.LoadFile(staged[0].Path);
            foreach (var (_, path) in staged.Skip(1))
                operation = operation.MergeFile(path);
            operation.Save(output);

            return await File.ReadAllBytesAsync(output, cancellationToken);
        }
        finally
        {
            try { Directory.Delete(workspace, recursive: true); }
            catch (IOException) { /* The temp directory outliving the request is not worth a 500. */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static byte[]? ToPdfPages(PdfBundleItem item)
    {
        var content = item.Content!;
        if (IsPdf(content)) return content;
        if (IsImage(content)) return RenderPhoto(content, item.Title, item.IsSnapshot, item.IsPortrait);
        return null;
    }

    /// <summary>
    /// Read and rewrite a single file, so a malformed one fails here rather than during the merge.
    /// </summary>
    private static string? Normalise(string source, string destination)
    {
        try
        {
            DocumentOperation.LoadFile(source).Save(destination);
            return destination;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Points per pixel when an image is printed at <c>PrintDpi</c>. 150 dpi is as coarse as a
    /// photo can be printed before it starts to look soft; anything larger than that is the
    /// printer inventing pixels.
    /// </summary>
    private const float PrintDpi = 150f;

    /// <summary>The most an image may take on an A4 page with a 24pt margin and the caption.</summary>
    private const float PageWidth = 540f, PageHeight = 740f;

    /// <summary>
    /// The box a passport or a photo is held to: about a passport's data page across, half a page
    /// or so down. A head-and-shoulders photo comes out roughly 12 × 16 cm, a passport scan
    /// about the width of the passport plus the desk it was photographed on.
    /// </summary>
    private const float SnapshotWidth = 400f, SnapshotHeight = 460f;

    /// <summary>
    /// The head-and-shoulders photo's box, about a fifth larger than the snapshot's. It is the
    /// picture a partner looks at first, and at the snapshot size it read as a thumbnail in the
    /// middle of an empty sheet.
    /// </summary>
    private const float PortraitWidth = 440f, PortraitHeight = 560f;

    /// <summary>
    /// The portrait may print a little coarser than <see cref="PrintDpi"/>, so an ordinary phone
    /// photo grows with the box instead of staying the size its pixels allowed at 150 dpi. 125 dpi
    /// is still finer than a face needs to look sharp on paper.
    /// </summary>
    private const float PortraitDpi = 125f;

    private static byte[]? RenderPhoto(byte[] content, string title, bool isSnapshot, bool isPortrait)
    {
        try
        {
            // Stretched across the page, a phone photo of 600-odd pixels was printing at around
            // 80 dpi and came out blurred. Now it is never printed larger than its own pixels allow,
            // and a passport or a photo never larger than the real thing would be.
            var (pixelWidth, pixelHeight) = PixelSize(content);
            var (width, height) = isPortrait
                ? PrintSize(pixelWidth, pixelHeight, PortraitWidth, PortraitHeight, PortraitDpi)
                : PrintSize(
                    pixelWidth, pixelHeight,
                    isSnapshot ? SnapshotWidth : PageWidth,
                    isSnapshot ? SnapshotHeight : PageHeight);

            return Document.Create(container => container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily(DocumentFonts.Chain).FontSize(9));

                page.Header().Text(title).FontSize(10).SemiBold();
                // In the middle of the sheet, not hung from the top of it: a photo smaller than the
                // page sat under the caption with the bottom two-thirds of the paper empty.
                page.Content().PaddingTop(10).AlignCenter().AlignMiddle()
                    // FitArea inside the box, so a photo whose stored orientation differs from
                    // its header still keeps its shape — it just comes out a little smaller.
                    .Width(width).Height(height).Image(content).FitArea();
            })).GeneratePdf();
        }
        catch
        {
            // An image the renderer cannot open is no more useful in the bundle than out of it.
            return null;
        }
    }

    /// <summary>
    /// The printed size of an image in points: its own pixels at <paramref name="dpi"/>, scaled
    /// down to fit the box when that is too big, keeping its shape. Never scaled up.
    /// </summary>
    public static (float Width, float Height) PrintSize(
        int pixelWidth, int pixelHeight, float maxWidth, float maxHeight, float dpi = PrintDpi)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) return (maxWidth, maxHeight);

        var width = pixelWidth * 72f / dpi;
        var height = pixelHeight * 72f / dpi;
        var scale = Math.Min(1f, Math.Min(maxWidth / width, maxHeight / height));
        return (width * scale, height * scale);
    }

    /// <summary>
    /// Width and height in pixels, read from the PNG or JPEG header; (0, 0) when it cannot be
    /// found, which prints the image as large as the page allows, as before.
    /// </summary>
    public static (int Width, int Height) PixelSize(byte[] c)
    {
        // PNG: the IHDR chunk is always first, width and height big-endian at bytes 16 and 20.
        if (c.Length >= 24 && c[0] == 0x89 && c[1] == 0x50)
            return (ReadInt32(c, 16), ReadInt32(c, 20));

        // JPEG: walk the segments to the start-of-frame, which holds height then width.
        if (c.Length >= 4 && c[0] == 0xFF && c[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < c.Length)
            {
                if (c[i] != 0xFF) { i++; continue; }
                var marker = c[i + 1];
                if (marker == 0xFF) { i++; continue; }
                if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) { i += 2; continue; }

                var length = (c[i + 2] << 8) | c[i + 3];
                var isFrame = marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;
                if (isFrame)
                    return ((c[i + 7] << 8) | c[i + 8], (c[i + 5] << 8) | c[i + 6]);
                if (length < 2) break;
                i += 2 + length;
            }
        }

        return (0, 0);
    }

    private static int ReadInt32(byte[] c, int at) =>
        (c[at] << 24) | (c[at + 1] << 16) | (c[at + 2] << 8) | c[at + 3];

    private static byte[] RenderDivider(string title) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(x => x.FontFamily(DocumentFonts.Chain));

            page.Content().AlignCenter().AlignMiddle().Column(column =>
            {
                column.Item().AlignCenter().Text(title).FontSize(20).Bold();
                column.Item().PaddingTop(6).AlignCenter()
                    .Text("Documents follow").FontSize(10).FontColor(Colors.Grey.Darken1);
            });
        })).GeneratePdf();

    // The declared content type is whatever the browser said at upload time, so the bytes are
    // asked directly instead.
    private static bool IsPdf(byte[] c) =>
        c.Length > 4 && c[0] == 0x25 && c[1] == 0x50 && c[2] == 0x44 && c[3] == 0x46;

    private static bool IsImage(byte[] c) =>
        (c.Length > 3 && c[0] == 0xFF && c[1] == 0xD8 && c[2] == 0xFF) ||
        (c.Length > 8 && c[0] == 0x89 && c[1] == 0x50 && c[2] == 0x4E && c[3] == 0x47);
}
