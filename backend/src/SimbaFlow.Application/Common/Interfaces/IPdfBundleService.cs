namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Turns a pile of separate files into one printable PDF.
///
/// A folder of loose files is fine for filing but useless at the counter: the desk prints the whole
/// packet, staples it and hands it over, and doing that from eight separate files is eight trips
/// through the print dialog.
/// </summary>
public interface IPdfBundleService
{
    /// <summary>
    /// One PDF, pages in the order given. Returns null when nothing in the list could be paged.
    /// </summary>
    Task<byte[]?> MergeAsync(IReadOnlyList<PdfBundleItem> items, CancellationToken cancellationToken = default);
}

/// <summary>
/// A piece of the bundle: either a file already on record, or a divider naming whose paperwork
/// starts here.
/// </summary>
public sealed record PdfBundleItem
{
    private PdfBundleItem() { }

    /// <summary>Printed on the divider page, or as the caption above a photo.</summary>
    public string Title { get; private init; } = string.Empty;

    /// <summary>The file's bytes, or null for a divider.</summary>
    public byte[]? Content { get; private init; }

    public bool IsDivider => Content is null;

    public static PdfBundleItem Divider(string title) => new() { Title = title };

    public static PdfBundleItem Document(string title, byte[] content) =>
        new() { Title = title, Content = content };
}
