using System.Text;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// Everything the bot says, in one place.
///
/// The bot talks to people who are standing in a queue at an embassy with one hand on a phone, so
/// a message has to be readable at a glance: the name first, the thing that changed second, and
/// nothing else competing for attention. Wording lives here rather than scattered through the
/// services so the voice stays the same whichever code path produced the message, and so the
/// Amharic is written next to the English it mirrors instead of drifting from it.
///
/// Text is formatted with Telegram's HTML parse mode, so every value that came from the database
/// goes through <see cref="Escape"/> — a candidate called "Tsehay &amp; Co" would otherwise break
/// the message rather than merely look wrong.
/// </summary>
public static class BotMessages
{
    /// <summary>Telegram's HTML parse mode only reserves these three.</summary>
    public static string Escape(string? value) =>
        (value ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

    private static string Or(string? value, bool amharic) =>
        string.IsNullOrWhiteSpace(value) ? (amharic ? "አልተመዘገበም" : "not recorded") : Escape(value);

    // ── Greeting and linking ────────────────────────────────────────────────

    public static string Welcome(bool amharic) => amharic
        ? "<b>SimbaFlow</b>\n\n"
          + "ይህ ቦት የኤጀንሲዎን እጩዎች ለማየት ነው።\n\n"
          + "ለመጀመር በድር መተግበሪያው <b>Settings</b> ውስጥ የማገናኛ ኮድ ይፍጠሩ፣ ከዚያ እዚህ ይላኩት:\n"
          + "<code>/link ኮድ</code>"
        : "<b>SimbaFlow</b>\n\n"
          + "This bot gives you your agency's candidates on your phone.\n\n"
          + "To start, open <b>Settings</b> in the web app, generate a link code, and send it here:\n"
          + "<code>/link CODE</code>";

    public static string AlreadyLinked(string fullName, bool amharic) => amharic
        ? $"እንኳን ደህና መጡ፣ <b>{Escape(fullName)}</b>።\n\nየፓስፖርት ቁጥር ወይም ስም ይላኩ፣ ወይም ከታች ያለውን ይጫኑ።"
        : $"Welcome back, <b>{Escape(fullName)}</b>.\n\nSend a passport number or a name, or use the buttons below.";

    public static string LinkSucceeded(string fullName, string? agencyName, bool amharic)
    {
        var agency = string.IsNullOrWhiteSpace(agencyName) ? null : Escape(agencyName);
        return amharic
            ? $"✅ ተገናኝቷል።\n\n<b>{Escape(fullName)}</b>{(agency is null ? "" : $"\n{agency}")}\n\n"
              + "አሁን የፓስፖርት ቁጥር ወይም ስም ልኩ እጩውን ያግኙ።"
            : $"✅ Linked.\n\n<b>{Escape(fullName)}</b>{(agency is null ? "" : $"\n{agency}")}\n\n"
              + "Send a passport number or a name to look someone up.";
    }

    public static string AskForLinkCode(bool amharic) => amharic
        ? "የማገናኛ ኮዱን ይላኩ፣ ለምሳሌ <code>/link ABCD2345</code>"
        : "Send the code from the web app, e.g. <code>/link ABCD2345</code>";

    // ── Candidate lookup ────────────────────────────────────────────────────

    /// <summary>
    /// One candidate, as a card. Passport is included because it is what staff quote to each other
    /// and to the embassy — a name on its own is not enough to act on when two people share one.
    /// </summary>
    public static string CandidateCard(
        string fullName,
        string passportNumber,
        string? stageName,
        string? statusLabel,
        string? countryOfTravel,
        bool amharic)
    {
        var sb = new StringBuilder();
        sb.Append($"<b>{Escape(fullName)}</b>\n");
        sb.Append($"<code>{Escape(passportNumber)}</code>\n\n");
        sb.Append(amharic ? $"ደረጃ: {Or(stageName, true)}\n" : $"Stage: {Or(stageName, false)}\n");
        sb.Append(amharic ? $"ሁኔታ: {Or(statusLabel, true)}\n" : $"Status: {Or(statusLabel, false)}\n");
        if (!string.IsNullOrWhiteSpace(countryOfTravel))
            sb.Append(amharic ? $"መድረሻ: {Escape(countryOfTravel)}\n" : $"Destination: {Escape(countryOfTravel)}\n");
        sb.Append('\n');
        sb.Append(amharic
            ? $"ሲቪ: <code>/cv {Escape(passportNumber)}</code>"
            : $"CV: <code>/cv {Escape(passportNumber)}</code>");
        return sb.ToString();
    }

    /// <summary>
    /// Telegram rejects a message over 4096 characters. Chunking at 3,500 leaves room for the
    /// markup and the trailing line without counting bytes precisely.
    /// </summary>
    private const int MaxMessageLength = 3500;

    /// <summary>
    /// Everyone who matched, as a numbered list split across as many messages as it takes.
    ///
    /// Typing a first name is a legitimate way to ask "who do we have called this?", so the answer
    /// is the whole set rather than the first few with an instruction to be more specific. The
    /// entries are one line each — name, passport, stage — because a card per person would turn
    /// thirty matches into a scroll nobody reads.
    /// </summary>
    public static IReadOnlyList<string> CandidateList(
        IReadOnlyList<(string FullName, string PassportNumber, string? StageName)> matches,
        string query,
        bool truncated,
        bool amharic)
    {
        var header = amharic
            ? $"<b>{matches.Count}</b> እጩዎች — “{Escape(query)}”\n\n"
            : $"<b>{matches.Count}</b> candidates matching “{Escape(query)}”\n\n";

        var pages = new List<string>();
        var sb = new StringBuilder(header);
        var index = 0;

        foreach (var m in matches)
        {
            index++;
            var line = new StringBuilder();
            line.Append($"{index}. <b>{Escape(m.FullName)}</b>\n");
            line.Append($"    <code>{Escape(m.PassportNumber)}</code>");
            if (!string.IsNullOrWhiteSpace(m.StageName))
                line.Append($" · {Escape(m.StageName)}");
            line.Append('\n');

            if (sb.Length + line.Length > MaxMessageLength)
            {
                pages.Add(sb.ToString().TrimEnd());
                sb = new StringBuilder();
            }
            sb.Append(line);
        }

        var tail = new StringBuilder();
        if (truncated)
            tail.Append(amharic
                ? $"\nከ{MaxResultsHint} በላይ ተገኝተዋል። ስሙን የበለጠ ይግለጹ።"
                : $"\nMore than {MaxResultsHint} matched. Add another part of the name to narrow it.");

        tail.Append(amharic
            ? "\nየሚፈልጉትን የፓስፖርት ቁጥር ይላኩ።"
            : "\nSend a passport number to open one.");

        sb.Append(tail);
        pages.Add(sb.ToString().TrimEnd());
        return pages;
    }

    /// <summary>Mirrors BotCandidateSearch.MaxResults; kept here so the wording stays in one file.</summary>
    private const int MaxResultsHint = 200;

    public static string CandidateNotFound(string query, bool amharic) => amharic
        ? $"<b>{Escape(query)}</b> የሚል እጩ አልተገኘም።\n\nየፓስፖርት ቁጥሩን ወይም የስሙን የተወሰነ ክፍል ይሞክሩ።"
        : $"No candidate matches <b>{Escape(query)}</b>.\n\nTry the passport number, or part of the name.";

    public static string AskForCandidate(bool amharic) => amharic
        ? "የፓስፖርት ቁጥር ወይም ስም ይላኩ።\nለምሳሌ: <code>EQ1030621</code> ወይም <code>Seada Mekonnen</code>"
        : "Send a passport number or a name.\nFor example: <code>EQ1030621</code> or <code>Seada Mekonnen</code>";

    public static string CvBeingPrepared(bool amharic) => amharic
        ? "ሲቪ በመዘጋጀት ላይ…"
        : "Preparing the CV…";

    // ── Change reports ──────────────────────────────────────────────────────

    /// <summary>
    /// A candidate moved along the pipeline.
    ///
    /// Written so the same message can be edited in place as the candidate moves again, which is
    /// why it reads as a current position ("now at Embassy") rather than as an event ("moved to
    /// Embassy"). Six hops in an afternoon then leave one line in the chat instead of six.
    /// </summary>
    public static string StageMoved(
        string candidateName,
        string? passportNumber,
        string? fromStageName,
        string? toStageName,
        string? movedBy,
        bool amharic)
    {
        var sb = new StringBuilder();
        sb.Append($"<b>{Escape(candidateName)}</b>");
        if (!string.IsNullOrWhiteSpace(passportNumber))
            sb.Append($"  <code>{Escape(passportNumber)}</code>");
        sb.Append('\n');

        var to = Or(toStageName, amharic);
        sb.Append(string.IsNullOrWhiteSpace(fromStageName)
            ? (amharic ? $"አሁን: {to}" : $"Now at {to}")
            : (amharic
                ? $"{Escape(fromStageName)} → {to}"
                : $"{Escape(fromStageName)} → {to}"));

        if (!string.IsNullOrWhiteSpace(movedBy))
            sb.Append(amharic ? $"\nበ {Escape(movedBy)}" : $"\nby {Escape(movedBy)}");

        return sb.ToString();
    }

    /// <summary>
    /// Turns an internal event key into something a person would say.
    ///
    /// The chat was showing raw keys — "FOZIYA SEID YIMER - departure.notified" — which is the
    /// system talking to itself in front of the user. An unmapped key falls back to the candidate's
    /// name alone rather than printing the key, because a key tells the reader nothing either way.
    /// </summary>
    public static string EventHeadline(string candidateName, string? passportNumber, string messageKey, bool amharic)
    {
        var what = messageKey?.Trim().ToLowerInvariant() switch
        {
            "departure.notified" => amharic ? "ለጉዞ ተነግሯል" : "has been told about their flight",
            "departure.confirmed" => amharic ? "ጉዞ ተረጋግጧል" : "has departed",
            "arrival.confirmed" => amharic ? "ደርሷል" : "has arrived",
            "ticket.booked" => amharic ? "ቲኬት ተይዟል" : "has a ticket booked",
            "visa.issued" => amharic ? "ቪዛ ተሰጥቷል" : "has their visa",
            "visa.rejected" => amharic ? "ቪዛ ተከልክሏል" : "was refused a visa",
            "medical.fit" => amharic ? "የሕክምና ምርመራ አልፏል" : "passed the medical",
            "medical.unfit" => amharic ? "የሕክምና ምርመራ አላለፈም" : "did not pass the medical",
            _ => null
        };

        var sb = new StringBuilder();
        sb.Append($"<b>{Escape(candidateName)}</b>");
        if (!string.IsNullOrWhiteSpace(passportNumber))
            sb.Append($"  <code>{Escape(passportNumber)}</code>");
        if (what is not null)
            sb.Append($"\n{what}");
        return sb.ToString();
    }

    // ── Help and errors ─────────────────────────────────────────────────────

    public static string Help(bool amharic) => amharic
        ? "<b>የምችላቸው</b>\n\n"
          + "• የፓስፖርት ቁጥር ወይም ስም ብቻ ይላኩ — እጩውን አገኛለሁ\n"
          + "• <code>/cv ፓስፖርት</code> — ሲቪ ማውረድ\n"
          + "• <code>/stats</code> — የኤጀንሲው ቁጥሮች\n"
          + "   <code>/stats week</code> · <code>/stats month</code> · <code>/stats embassy</code>\n"
          + "• <code>/lang en</code> — ወደ እንግሊዝኛ\n\n"
          + "እጩ ደረጃ ሲቀይር እዚህ አሳውቃለሁ — የራስዎን ለውጥ አላሳውቅም።"
        : "<b>What I can do</b>\n\n"
          + "• Just send a passport number or a name — I'll find the candidate\n"
          + "• <code>/cv PASSPORT</code> — get their CV as a PDF\n"
          + "• <code>/stats</code> — where your agency stands\n"
          + "   <code>/stats week</code> · <code>/stats month</code> · <code>/stats embassy</code>\n"
          + "• <code>/lang am</code> — switch to Amharic\n\n"
          + "I'll tell you when a candidate moves stage — never for changes you made yourself.";

    public static string NotUnderstood(bool amharic) => amharic
        ? "አልገባኝም። የፓስፖርት ቁጥር ወይም ስም ይላኩ፣ ወይም ❓ Help ይጫኑ።"
        : "I didn't catch that. Send a passport number or a name, or tap ❓ Help.";

    public static string NoAgency(bool amharic) => amharic
        ? "ይህ መለያ ከኤጀንሲ ጋር አልተገናኘም፣ ስለዚህ የሚታይ ነገር የለም።\nአስተዳዳሪዎን ያነጋግሩ።"
        : "This account isn't attached to an agency, so there's nothing to look up.\nAsk your administrator to assign you to one.";

    public static string NoStatsPermission(bool amharic) => amharic
        ? "የኤጀንሲውን አጠቃላይ ቁጥሮች ለማየት ፈቃድ የለዎትም።"
        : "You don't have permission to see agency-wide numbers.";

    public static string WebAppOnly(bool amharic) => amharic
        ? "ይህ ለውጥ ከድር መተግበሪያው መደረግ አለበት — የሚፈለገውን ዝርዝር ሁሉ ለመያዝ።"
        : "That change has to be made in the web app — it needs details this chat can't collect safely.";

    public static string LanguageChoices(bool amharic) => amharic
        ? "<code>/lang en</code> ለእንግሊዝኛ ወይም <code>/lang am</code> ለአማርኛ ይላኩ።"
        : "Send <code>/lang en</code> for English or <code>/lang am</code> for Amharic.";

    public static string LanguageUpdated(bool amharic) => amharic
        ? "ቋንቋ ወደ አማርኛ ተቀይሯል።"
        : "Language set to English.";

    public static string SomethingWentWrong(bool amharic) => amharic
        ? "አልተሳካም። እባክዎ እንደገና ይሞክሩ።"
        : "That didn't work. Please try again in a moment.";
}
