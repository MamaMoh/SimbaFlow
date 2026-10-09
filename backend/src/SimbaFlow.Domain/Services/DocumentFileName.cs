using System.Globalization;
using System.Text;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// What a generated document is called once it leaves the system.
///
/// Every generator named its download after something the machine cared about and nobody else
/// did: the CV came out as cv_{guid}.pdf, the bundle as documents_{timestamp}.pdf, and a form
/// opened in a browser tab and then saved arrived in Downloads under the object URL's identifier —
/// b5e6ecb2-041c-41e7-9865-eca1908e5cac.pdf. An agency printing paperwork for twenty candidates in
/// a morning ends up with twenty of those in one folder and no way to tell them apart short of
/// opening each one.
///
/// So a document is named after the person it is about and the thing it is: "Almaz Debela Kebede -
/// CV.pdf". That sorts by candidate, which is how the folder is actually read.
///
/// The name is a person's name, so it may be Amharic, and it is passed through rather than
/// transliterated — Content-Disposition carries UTF-8 by RFC 6266 and every browser in use
/// handles it. Only the characters that break a path are removed.
/// </summary>
public static class DocumentFileName
{
    /// <summary>Characters no filesystem in play will accept, plus the ones that confuse a shell.</summary>
    private static readonly char[] Illegal = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// How long a name may run before it is cut. Generous — the limit exists so a pasted essay in
    /// the name field cannot produce a path the filesystem refuses, not to keep names short.
    /// </summary>
    private const int MaxNameLength = 80;

    /// <param name="candidateName">The candidate's full name; blank falls back to the kind alone.</param>
    /// <param name="kind">What the document is, as it should read in the name: "CV", "Contract".</param>
    public static string For(string? candidateName, string kind, string extension = "pdf")
    {
        var person = Clean(candidateName);
        return person.Length == 0
            ? $"{kind}.{extension}"
            : $"{person} - {kind}.{extension}";
    }

    private static string Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var sb = new StringBuilder(name.Length);
        var lastWasSpace = false;

        foreach (var ch in name.Trim())
        {
            // Whitespace is examined before anything is dropped. A tab or a newline is a control
            // character as well as whitespace, and discarding it outright would run the words on
            // either side of it together — a name pasted across two lines came out as one word.
            // A run of it becomes a single space, so a name typed with a stray double space gives
            // the same filename as the same name typed once.
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace && sb.Length > 0) sb.Append(' ');
                lastWasSpace = true;
                continue;
            }

            if (Illegal.Contains(ch) || char.GetUnicodeCategory(ch) == UnicodeCategory.Control)
                continue;

            sb.Append(ch);
            lastWasSpace = false;
        }

        var cleaned = sb.ToString().Trim();

        // A name ending in a dot is a valid string and an invalid filename on Windows, and these
        // get opened on Windows desks far more often than anywhere else.
        cleaned = cleaned.TrimEnd('.', ' ');

        return cleaned.Length <= MaxNameLength ? cleaned : cleaned[..MaxNameLength].TrimEnd();
    }
}
