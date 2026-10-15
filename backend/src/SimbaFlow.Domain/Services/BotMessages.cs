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
    /// <param name="withCvHint">
    /// Whether to end with "CV: /cv PASSPORT". On a lookup that is the next thing you want and
    /// the card is the only place that says how to ask for it. On the answer to /cv itself the
    /// document is attached to this very message, so the line was telling someone holding the
    /// CV how to request the CV.
    /// </param>
    /// <param name="profileUrl">
    /// The candidate's page in the web application. A card answers the question you asked and
    /// raises the next one — the address, the next of kin, the documents — and the only way on
    /// from here was to put the phone down and find the right row in a browser.
    /// </param>
    public static string CandidateCard(
        string fullName,
        string passportNumber,
        string? stageName,
        string? statusLabel,
        string? countryOfTravel,
        bool amharic,
        bool withCvHint = true,
        string? profileUrl = null)
    {
        var sb = new StringBuilder();
        sb.Append($"<b>{Escape(fullName)}</b>\n");
        sb.Append($"<code>{Escape(passportNumber)}</code>\n\n");
        sb.Append(amharic ? $"ደረጃ: {Or(stageName, true)}\n" : $"Stage: {Or(stageName, false)}\n");
        sb.Append(amharic ? $"ሁኔታ: {Or(statusLabel, true)}\n" : $"Status: {Or(statusLabel, false)}\n");
        if (!string.IsNullOrWhiteSpace(countryOfTravel))
            sb.Append(amharic ? $"መድረሻ: {Escape(countryOfTravel)}\n" : $"Destination: {Escape(countryOfTravel)}\n");
        if (withCvHint)
        {
            sb.Append('\n');
            sb.Append(amharic
                ? $"ሲቪ: <code>/cv {Escape(passportNumber)}</code>"
                : $"CV: <code>/cv {Escape(passportNumber)}</code>");
        }
        if (!string.IsNullOrWhiteSpace(profileUrl))
        {
            sb.Append('\n');
            sb.Append(amharic
                ? $"<a href=\"{Escape(profileUrl)}\">ሙሉ መረጃ ይክፈቱ</a>"
                : $"<a href=\"{Escape(profileUrl)}\">Open the full record</a>");
        }
        return sb.ToString().TrimEnd();
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
    public static string EventHeadline(
        string candidateName,
        string? passportNumber,
        string messageKey,
        bool amharic,
        string? by = null)
    {
        var what = messageKey?.Trim().ToLowerInvariant() switch
        {
            "candidate.registered" => amharic ? "ተመዝግቧል" : "has been registered",
            "departure.notified" => amharic ? "ለጉዞ ተነግሯል" : "has been told about their flight",
            "departure.confirmed" => amharic ? "ጉዞ ተረጋግጧል" : "has departed",
            "arrival.confirmed" => amharic ? "ደርሷል" : "has arrived",
            "ticket.booked" => amharic ? "ቲኬት ተይዟል" : "has a ticket booked",
            "visa.issued" => amharic ? "ቪዛ ተሰጥቷል" : "has their visa",
            "visa.rejected" => amharic ? "ቪዛ ተከልክሏል" : "was refused a visa",
            "medical.fit" => amharic ? "የሕክምና ምርመራ አልፏል" : "passed the medical",
            "medical.unfit" => amharic ? "የሕክምና ምርመራ አላለፈም" : "did not pass the medical",
            // The two that are not good news, and the two an owner most wants to hear the
            // same day rather than in tomorrow's digest.
            "exception.returned" => amharic ? "ተመልሷል" : "has been returned",
            "exception.runaway" => amharic ? "ጠፍቷል" : "has been reported missing",
            _ => null
        };

        var sb = new StringBuilder();
        sb.Append($"<b>{Escape(candidateName)}</b>");
        if (!string.IsNullOrWhiteSpace(passportNumber))
            sb.Append($"  <code>{Escape(passportNumber)}</code>");
        if (what is not null)
            sb.Append($"\n{what}");
        // Who did it, where the caller knows — the same courtesy the stage message pays, so
        // nobody has to open the web application to find out who to ask about it.
        if (!string.IsNullOrWhiteSpace(by))
            sb.Append(amharic ? $" — በ {Escape(by)}" : $" — by {Escape(by)}");
        return sb.ToString();
    }

    /// <summary>
    /// The candidate's page in the web application, or null when nobody has told the server
    /// what address it answers on — better no link than one that goes nowhere.
    /// </summary>
    public static string? ProfileUrl(string? appBaseUrl, Guid candidateId) =>
        string.IsNullOrWhiteSpace(appBaseUrl)
            ? null
            : $"{appBaseUrl.TrimEnd('/')}/candidates/{candidateId}";

    // ── Lists the desk asks for ─────────────────────────────────────────────

    /// <summary>
    /// One line per person, with how long they have been standing there.
    ///
    /// The same shape serves a stage list, the overdue list and the expiry list, because they
    /// are the same question asked three ways: who, and for how long. Paged like the search
    /// results, for the same reason — Telegram will not take a message over 4096 characters.
    /// </summary>
    public static IReadOnlyList<string> PeopleList(
        string title,
        IReadOnlyList<(string FullName, string? PassportNumber, string? Detail)> rows,
        string? emptyLine,
        bool amharic)
    {
        if (rows.Count == 0)
            return [emptyLine ?? (amharic ? "ምንም የለም።" : "Nothing here.")];

        var pages = new List<string>();
        var sb = new StringBuilder($"<b>{Escape(title)}</b> — {rows.Count}\n\n");
        var index = 0;

        foreach (var r in rows)
        {
            index++;
            var line = new StringBuilder();
            line.Append($"{index}. <b>{Escape(r.FullName)}</b>\n");
            if (!string.IsNullOrWhiteSpace(r.PassportNumber))
                line.Append($"    <code>{Escape(r.PassportNumber)}</code>");
            if (!string.IsNullOrWhiteSpace(r.Detail))
                line.Append(string.IsNullOrWhiteSpace(r.PassportNumber)
                    ? $"    {Escape(r.Detail)}"
                    : $" · {Escape(r.Detail)}");
            line.Append('\n');

            if (sb.Length + line.Length > MaxMessageLength)
            {
                pages.Add(sb.ToString().TrimEnd());
                sb = new StringBuilder();
            }
            sb.Append(line);
        }

        pages.Add(sb.ToString().TrimEnd());
        return pages;
    }

    /// <summary>Which stage? — asked when the button was pressed with nothing after it.</summary>
    public static string AskForStage(IReadOnlyList<string> stageNames, bool amharic)
    {
        var list = string.Join(" · ", stageNames.Select(n => $"<code>{Escape(n)}</code>"));
        return amharic
            ? $"የትኛው ደረጃ? ስሙን ይላኩ።\n\n{list}"
            : $"Which stage? Send the name.\n\n{list}";
    }

    public static string NothingOverdue(bool amharic) => amharic
        ? $"✅ ከ{WorkQueueRules.OverdueThresholdDays} ቀን በላይ በአንድ ደረጃ የቆየ የለም።"
        : $"✅ Nobody has been in one stage longer than {WorkQueueRules.OverdueThresholdDays} days.";

    public static string NothingExpiring(bool amharic) => amharic
        ? $"✅ በ{WorkQueueRules.ExpirySoonDays} ቀን ውስጥ የሚያልቅ ፓስፖርት የለም።"
        : $"✅ No passport runs out within {WorkQueueRules.ExpirySoonDays} days.";

    /// <summary>How long a passport has left, or how long ago it went.</summary>
    public static string PassportAge(int daysLeft, bool amharic) => daysLeft < 0
        ? (amharic ? $"ከ{-daysLeft} ቀን በፊት አልቋል" : $"expired {PluralText.Count(-daysLeft, "day")} ago")
        : (amharic ? $"በ{daysLeft} ቀን ያልቃል" : $"expires in {PluralText.Count(daysLeft, "day")}");

    /// <summary>How long they have been standing here, where the heading already says where.</summary>
    public static string DaysHere(int days, bool amharic) => amharic
        ? $"{days} ቀን"
        : PluralText.Count(days, "day");

    public static string DaysInStage(int days, string? stageName, bool amharic) => amharic
        ? $"{days} ቀን በ{Or(stageName, true)}"
        : $"{PluralText.Count(days, "day")} in {Or(stageName, false)}";

    /// <summary>The three numbers My work opens with, as one message.</summary>
    public static string MyWork(int overdue, int expiring, int exceptions, bool amharic)
    {
        if (overdue == 0 && expiring == 0 && exceptions == 0)
            return amharic ? "✅ ምንም የሚጠብቅ የለም።" : "✅ Nothing waiting on you.";

        var sb = new StringBuilder(amharic ? "<b>የእርስዎ ተግባራት</b>\n\n" : "<b>Your queue</b>\n\n");
        if (exceptions > 0)
            sb.Append(amharic
                ? $"⚠️ {exceptions} ክፍት ችግር\n"
                : $"⚠️ {PluralText.Count(exceptions, "open case")}\n");
        if (overdue > 0)
            sb.Append(amharic
                ? $"⏰ {overdue} የቆዩ — <code>/overdue</code>\n"
                : $"⏰ {overdue} overdue — <code>/overdue</code>\n");
        if (expiring > 0)
            sb.Append(amharic
                ? $"🛂 {expiring} የሚያልቅ ፓስፖርት — <code>/expiring</code>\n"
                : $"🛂 {expiring} passports running out — <code>/expiring</code>\n");
        return sb.ToString().TrimEnd();
    }

    /// <summary>The morning digest: what the day starts with.</summary>
    public static string Digest(
        int registeredYesterday, int overdue, int expiring, int openCases, bool amharic)
    {
        var sb = new StringBuilder(amharic ? "<b>☀️ ጠዋት</b>\n\n" : "<b>☀️ This morning</b>\n\n");

        if (registeredYesterday > 0)
            sb.Append(amharic
                ? $"🆕 ትናንት {registeredYesterday} ተመዝግበዋል\n"
                : $"🆕 {PluralText.Count(registeredYesterday, "registration")} yesterday\n");
        if (openCases > 0)
            sb.Append(amharic
                ? $"⚠️ {openCases} ክፍት ችግር\n"
                : $"⚠️ {PluralText.Count(openCases, "open case")}\n");
        if (overdue > 0)
            sb.Append(amharic
                ? $"⏰ {overdue} የቆዩ — <code>/overdue</code>\n"
                : $"⏰ {overdue} overdue — <code>/overdue</code>\n");
        if (expiring > 0)
            sb.Append(amharic
                ? $"🛂 {expiring} የሚያልቅ ፓስፖርት — <code>/expiring</code>\n"
                : $"🛂 {expiring} passports running out — <code>/expiring</code>\n");

        return sb.ToString().TrimEnd();
    }

    // ── What you hear about ─────────────────────────────────────────────────

    public static string Muted(bool amharic) => amharic
        ? "🔕 ማሳወቂያ ቆሟል። መፈለግና ሲቪ ማውረድ እንደተለመደው ይሰራል።\n\nለመመለስ <code>/unmute</code>።"
        : "🔕 Notifications off. Looking people up and fetching CVs still works.\n\n"
          + "Turn them back on with <code>/unmute</code>.";

    public static string Unmuted(bool amharic) => amharic
        ? "🔔 ማሳወቂያ ተመልሷል።"
        : "🔔 Notifications are back on.";

    public static string Watching(IReadOnlyList<string> stages, bool amharic)
    {
        if (stages.Count == 0)
            return amharic
                ? "ሁሉንም ደረጃ እየተከታተሉ ነው።\n\nለመገደብ: <code>/watch embassy, lmis</code>"
                : "You hear about every stage.\n\nTo narrow it: <code>/watch embassy, lmis</code>";

        var list = string.Join(", ", stages.Select(Escape));
        return amharic
            ? $"እየተከታተሉ ያሉት: <b>{list}</b>\n\nሁሉንም ለመመለስ <code>/watch all</code>"
            : $"You hear about: <b>{list}</b>\n\nBack to everything with <code>/watch all</code>";
    }

    // ── Unlinking ───────────────────────────────────────────────────────────

    public static string UnlinkConfirm(bool amharic) => amharic
        ? "ይህን ውይይት ከመለያዎ ልለያየው? <code>/unlink yes</code> ይላኩ።\n\nከዚያ በኋላ ማሳወቂያ አይደርስዎትም፤ እንደገና ለማገናኘት አዲስ ኮድ ያስፈልጋል።"
        : "Disconnect this chat from your account? Send <code>/unlink yes</code>.\n\n"
          + "Notifications stop on this phone, and linking again needs a fresh code from Settings.";

    /// <summary>
    /// Says which chat went, and what is left.
    ///
    /// An account may have several phones linked, and <c>/unlink</c> disconnects the one it was
    /// sent from. "Disconnected" on its own would read as the whole account having gone quiet,
    /// which for somebody carrying two numbers is the wrong thing to believe.
    /// </summary>
    public static string Unlinked(bool amharic, int remaining = 0)
    {
        if (remaining <= 0)
            return amharic
                ? "ተለያይቷል። ለማገናኘት ከSimbaFlow ቅንብሮች አዲስ ኮድ ይውሰዱ።"
                : "Disconnected. To link again, take a fresh code from Settings in SimbaFlow.";

        return amharic
            ? $"ይህ ውይይት ተለያይቷል። መለያዎ ላይ {remaining} ሌላ ቴሌግራም አሁንም ተገናኝቷል።"
            : $"This chat is disconnected. Your account still has "
              + $"{PluralText.Count(remaining, "other Telegram account")} linked.";
    }

    /// <summary>The agency has switched the bot off for everyone.</summary>
    public static string BotTurnedOff(bool amharic) => amharic
        ? "ይህ ኤጀንሲ ቦቱን አጥፍቶታል። አስተዳዳሪዎን ያነጋግሩ።"
        : "This agency has turned the bot off. Ask your administrator to switch it back on.";

    // ── Help and errors ─────────────────────────────────────────────────────

    public static string Help(bool amharic) => amharic
        ? "<b>የምችላቸው</b>\n\n"
          + "• የፓስፖርት ቁጥር ወይም ስም ብቻ ይላኩ — እጩውን አገኛለሁ\n"
          + "• <code>/cv ፓስፖርት</code> — ሲቪ ማውረድ\n"
          + "• <code>/stats</code> — የኤጀንሲው ቁጥሮች\n"
          + "   <code>/stats week</code> · <code>/stats month</code> · <code>/stats embassy</code>\n"
          + "• <code>/stage embassy</code> — በደረጃው ያሉትን ስም በስም\n"
          + "• <code>/overdue</code> — በአንድ ደረጃ የቆዩ\n"
          + "• <code>/expiring</code> — የሚያልቅ ፓስፖርት\n"
          + "• <code>/me</code> — የእርስዎ ተግባራት\n"
          + "• <code>/mute</code> — ማሳወቂያ ማቆም · <code>/watch embassy</code> — የተወሰነ ደረጃ ብቻ\n"
          + "• <code>/lang en</code> — ወደ እንግሊዝኛ\n"
          + "• <code>/unlink</code> — ይህን ውይይት መለየት\n\n"
          + "እጩ ሲመዘገብ፣ ደረጃ ሲቀይር፣ ሕክምና፣ ቪዛ፣ ቲኬት፣ ጉዞና መድረስ እዚህ አሳውቃለሁ — የራስዎን ለውጥ አላሳውቅም።"
        : "<b>What I can do</b>\n\n"
          + "• Just send a passport number or a name — I'll find the candidate\n"
          + "• <code>/cv PASSPORT</code> — get their CV as a PDF\n"
          + "• <code>/stats</code> — where your agency stands\n"
          + "   <code>/stats week</code> · <code>/stats month</code> · <code>/stats embassy</code>\n"
          + "• <code>/stage embassy</code> — who is standing in a stage, by name\n"
          + "• <code>/overdue</code> — anyone stuck too long in one stage\n"
          + "• <code>/expiring</code> — passports running out\n"
          + "• <code>/me</code> — your own queue\n"
          + "• <code>/mute</code> — stop notifications · <code>/watch embassy</code> — only certain stages\n"
          + "• <code>/lang am</code> — switch to Amharic\n"
          + "• <code>/unlink</code> — disconnect this chat\n\n"
          + "I'll tell you when a candidate is registered or moves stage, and about medicals, "
          + "visas, tickets, departures and arrivals — never for changes you made yourself.";

    /// <summary>
    /// The "what else can /stats do" line.
    ///
    /// This used to end with a literal &lt;stage&gt;, which Telegram read as an opening tag and
    /// rejected the whole message for — /stats answered with silence. Placeholders are written in
    /// braces now, and the examples are real stage-less words so there is nothing to parse.
    /// </summary>
    public static string StatsFooter(bool amharic) => amharic
        ? "ተጨማሪ: <code>/stats week</code> · <code>/stats month</code> · <code>/stats year</code> · <code>/stats embassy</code>"
        : "More: <code>/stats week</code> · <code>/stats month</code> · <code>/stats year</code> · <code>/stats embassy</code>";

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
