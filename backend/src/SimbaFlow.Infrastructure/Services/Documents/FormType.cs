using QuestPDF.Helpers;

namespace SimbaFlow.Infrastructure.Services.Documents;

/// <summary>
/// One type scale for every CV layout.
///
/// The six layouts are six arrangements of the same labelled row, but each part of the code had
/// its own numbers for how that row is set — the shared primitives, the layouts' own Row3, and
/// three layouts with hand-sized cells as small as 6pt. Nothing chose those; they were tuned
/// separately, at different times, and the same form printed at several sizes depending on which
/// layout an agency had picked. The layouts decide what goes where, not how big it is.
///
/// The sizes are the ones the agency asked for, in the px they asked in: 13, 13.5, 16 and 14 at
/// the CSS 96dpi convention, which is 0.75 of each in the points a PDF is measured in.
///
/// They did not fit at first. At these sizes layouts 1, 2 and 3 ran onto a second page, and the
/// gap looked like about a third of a page — far more than margins or padding could give back.
///
/// <see cref="LineHeight"/> is where the room came from, and it is the only reason this scale is
/// possible. A row is as tall as the tallest script in it, and that is the Arabic: Noto Sans
/// Arabic declares a line height near one and a half times its size, because Arabic set as running
/// text needs room for the marks that sit above and below the line. These cells are not running
/// text. They hold one short label with no stacked diacritics, so that leading was empty space on
/// every row of a form that has thirty of them. Pinning the line box to one em spends it on the
/// letters instead.
///
/// It is spent, though — there is no slack left in it. Raising these numbers again will push the
/// longer layouts onto a second page, and the next place to look would be the blank rows these
/// forms print whether or not the candidate has an answer.
///
/// Two things to know before changing them. Where a label does wrap to a second line, the two
/// lines now sit closer together; that was checked against the Arabic at 2.85× and nothing clips,
/// but it is worth looking again rather than assuming. And the font these heights come from is
/// whatever the machine has — Noto on the server, Arial Unicode on a developer's Mac — so a local
/// render is not the same document. CvRenderTests asserts one page; run it with the server's font
/// before believing it.
/// </summary>
internal static class FormType
{
    /// <summary>The label columns, left (English) and right (Arabic). 13px.</summary>
    internal const float Label = 9.75f;

    /// <summary>The answer, between them. 13.5px.</summary>
    internal const float Value = 10.125f;

    /// <summary>A section heading, reversed out of the bar. 16px.</summary>
    internal const float Heading = 12f;

    /// <summary>Anything on the page not given a size of its own. 14px.</summary>
    internal const float PageDefault = 10.5f;

    /// <summary>
    /// The line box, as a multiple of the font size, replacing the font's own line spacing.
    /// See the note above — this is what pays for the sizes.
    /// </summary>
    internal const float LineHeight = 1f;

    internal const float RowPadding = 2.4f;
    internal const float BarPadding = 3.5f;

    internal static readonly string LabelColor = Colors.Grey.Darken4;
}
