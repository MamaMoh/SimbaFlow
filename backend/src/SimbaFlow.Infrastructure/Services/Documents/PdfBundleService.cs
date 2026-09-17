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
        if (IsImage(content)) return RenderPhoto(content, item.Title);
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

    private static byte[]? RenderPhoto(byte[] content, string title)
    {
        try
        {
            return Document.Create(container => container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily(DocumentFonts.Chain).FontSize(9));

                page.Header().Text(title).FontSize(10).SemiBold();
                page.Content().PaddingTop(10).AlignCenter().AlignMiddle().Image(content).FitArea();
            })).GeneratePdf();
        }
        catch
        {
            // An image the renderer cannot open is no more useful in the bundle than out of it.
            return null;
        }
    }

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
