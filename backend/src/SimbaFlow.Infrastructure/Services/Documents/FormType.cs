using QuestPDF.Helpers;

namespace SimbaFlow.Infrastructure.Services.Documents;

/// <summary>
/// One type scale for every CV layout.
///
/// The six layouts are six arrangements of the same labelled row, but each half of the code had
/// its own numbers for how that row is set: the shared primitives used 7.2pt labels on 8pt values
/// with 3pt of padding, while the layouts' own Row3 used 6.8 on 7.8 with 2.2. Nothing chose those
/// — they were tuned separately, at different times, and the result was that the same form printed
/// at two sizes depending on which layout the agency had picked. Putting the numbers here is what
/// stops that happening again; the layouts decide what goes where, not how big it is.
///
/// Labels were grey and regular against black bold values, which read as faint beside the printed
/// forms these are meant to match. They are SemiBold and near-black now: still a step below the
/// value, so the eye finds the answer first, but legible on a photocopy.
///
/// The sizes are small and move in small steps because the forms are dense and must stay on one
/// sheet. Raising a size costs height on every row below it, and the longest column on these
/// layouts is about thirty rows, so a point here is thirty points there — <see cref="Value"/> went
/// up by 0.4 for that reason and not because 0.4 was the attractive number.
///
/// A caution on measuring that: the row's height is set by whichever script in it is tallest, and
/// Arabic usually is. The Arabic font is whatever the machine has (see DocumentFonts) — Noto on
/// the server, Arial Unicode on a developer's Mac — and their line metrics differ enough that the
/// same layout renders about two points shorter per row locally than it does in production. So a
/// local render that fits is evidence, not proof. CvRenderTests asserts one page, which catches a
/// change that is plainly too big; a change that is marginal has to be checked on a real one.
/// </summary>
internal static class FormType
{
    /// <summary>The label columns, left (English) and right (Arabic).</summary>
    internal const float Label = 7.2f;

    /// <summary>The answer, between them.</summary>
    internal const float Value = 8.2f;

    /// <summary>A section heading, reversed out of the bar.</summary>
    internal const float Heading = 8.8f;

    internal const float RowPadding = 2.4f;
    internal const float BarPadding = 3.5f;

    internal static readonly string LabelColor = Colors.Grey.Darken4;
}
