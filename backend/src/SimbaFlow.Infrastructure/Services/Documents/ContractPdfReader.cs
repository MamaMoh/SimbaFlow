using System.Globalization;
using System.Text;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace SimbaFlow.Infrastructure.Services.Documents;

/// <summary>
/// Pulls the English column out of a Saudi employment contract so the parser can read it.
///
/// The form is two columns of the same text, English on the left and Arabic on the right, and a
/// PDF has no notion of either — it has glyphs at coordinates. Read naively, every line comes back
/// as the English sentence with its Arabic translation welded onto the end, and "Name: Example
/// Resources Company" is followed by the same thing again in Arabic. So the split is done on the
/// only thing that reliably distinguishes the columns, which is where on the page the words are.
///
/// Three things about that are worth knowing before changing it.
///
/// The column boundary is the middle of the page. These contracts are generated from one template
/// and the gutter sits on the centre line; a word is English-column if it starts left of it.
///
/// A "line" is a group of words sharing a baseline, which is how a PDF stores them — there are no
/// line breaks in the file. Baselines are compared rounded to the point, because the two columns
/// are typeset separately and a fraction of a point apart.
///
/// And the left column is not all English. The sponsor's street is often written in Arabic even
/// there, and it arrives mangled, in more than one way depending on where in the form it is.
/// <see cref="Readable"/> deals with that. It matters because that street goes into the sponsor's
/// address field and someone has to read it back.
/// </summary>
public sealed class ContractPdfReader : IContractReaderService
{
    public SaudiContractDetails Read(Stream pdf)
    {
        try
        {
            using var buffer = new MemoryStream();
            pdf.CopyTo(buffer);
            return SaudiContractParser.Parse(EnglishColumn(buffer.ToArray()));
        }
        catch (Exception)
        {
            // A file that is not a PDF, or a PDF with no text layer because it was scanned, is a
            // thing the desk will hand us. It reads as "nothing recognised" and they type it in.
            return new SaudiContractDetails();
        }
    }

    private static string EnglishColumn(byte[] bytes)
    {
        using var document = PdfDocument.Open(bytes);
        var text = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            var gutter = page.Width / 2;

            var lines = page.GetWords(NearestNeighbourWordExtractor.Instance)
                .Where(w => w.BoundingBox.Left < gutter)
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom))
                .OrderByDescending(g => g.Key)
                .Select(g => g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text).ToList());

            foreach (var line in lines) text.AppendLine(Readable(line));
        }

        return text.ToString();
    }

    /// <summary>
    /// One line of words, in the order they sit across the page, turned into the order it is read in.
    ///
    /// Two separate things are back to front, and the form mixes both within one file.
    ///
    /// The word order. Arabic runs right to left, so the first word of a phrase is the rightmost
    /// one and sorting by position puts the phrase backwards: "حي العليا" arrives as "العليا حي".
    /// Each run of Arabic words is therefore reversed as a run. Numbers are left where they sit —
    /// a house number or a postcode inside an Arabic address is written left to right like
    /// anywhere else, and it is not part of the run.
    ///
    /// The letters inside a word, but only where the generator wrote presentation forms — the
    /// per-position glyph shapes in the U+FB50 and U+FE70 blocks. Those are laid down one glyph at
    /// a time in the order they are drawn, which is right to left, so the word is stored backwards;
    /// normalising gives the letters back but not their order. Elsewhere in the same contract the
    /// Arabic is stored as plain letters in reading order and must be left alone. A presentation
    /// form anywhere on the line is what tells the two apart, because a field is typeset in one
    /// font and one direction.
    /// </summary>
    private static string Readable(IReadOnlyList<string> words)
    {
        var visual = words.Any(HasPresentationForm);

        var tokens = words
            .Select(w => w.Normalize(NormalizationForm.FormKC))
            .Select(w => visual && IsArabic(w) ? Reversed(w) : w)
            .ToList();

        for (var i = 0; i < tokens.Count;)
        {
            if (!HasArabicLetter(tokens[i])) { i++; continue; }

            var run = i;
            while (run < tokens.Count && HasArabicLetter(tokens[run])) run++;
            tokens.Reverse(i, run - i);
            i = run;
        }

        return string.Join(" ", tokens);
    }

    /// <summary>
    /// One word, read back to front.
    ///
    /// Digits do not reverse. Flipping them changes the number: 12251 became 15221, which is the
    /// kind of wrong that gets saved because it still looks like a postcode. So the word is
    /// reversed and then each run of digits is turned back.
    ///
    /// By text element rather than by char, because reversing a combining mark away from the
    /// letter it sits on renders as nonsense.
    /// </summary>
    private static string Reversed(string word)
    {
        var elements = new List<string>();
        var walker = StringInfo.GetTextElementEnumerator(word);
        while (walker.MoveNext()) elements.Add((string)walker.Current);
        elements.Reverse();

        for (var i = 0; i < elements.Count;)
        {
            if (!IsDigit(elements[i])) { i++; continue; }

            var run = i;
            while (run < elements.Count && IsDigit(elements[run])) run++;
            elements.Reverse(i, run - i);
            i = run;
        }

        return string.Concat(elements);
    }

    private static bool IsDigit(string element) =>
        element.Length == 1 && char.IsDigit(element[0]);

    private static bool HasPresentationForm(string word) =>
        word.Any(c => c is >= '\uFB50' and <= '\uFDFF' or >= '\uFE70' and <= '\uFEFF');

    /// <summary>A letter, as against the digits and commas that sit inside an Arabic address.</summary>
    private static bool HasArabicLetter(string word) =>
        word.Any(c => c is >= '\u0620' and <= '\u064A'
                          or >= '\u066E' and <= '\u06D3'
                          or >= '\u0750' and <= '\u077F'
                          or >= '\uFB50' and <= '\uFDFF'
                          or >= '\uFE70' and <= '\uFEFF');

    private static bool IsArabic(string word) =>
        word.Any(c => c is >= '\u0600' and <= '\u06FF'
                          or >= '\u0750' and <= '\u077F'
                          or >= '\uFB50' and <= '\uFDFF'
                          or >= '\uFE70' and <= '\uFEFF');
}
