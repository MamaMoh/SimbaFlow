namespace SimbaFlow.Domain.Services;

/// <summary>
/// Maps whatever a user actually types in Telegram onto a canonical command.
///
/// Field staff are not going to memorise slash syntax, so three things all have to work:
/// tapping a keyboard button ("📊 Stats"), typing a command (/stats), and simply typing a
/// passport number or a name with no command at all — which is what people do by instinct.
/// </summary>
public enum BotCommand
{
    /// <summary>Unlinked/first contact — explain how to link.</summary>
    Start,
    Help,
    Link,
    Language,
    Status,
    Cv,
    Stats,
    /// <summary>Bare text that is not a command: treat it as a candidate lookup.</summary>
    Search,
    Unknown
}

public readonly record struct BotCommandParse(BotCommand Command, string Argument);

public static class BotCommandRules
{
    // Button captions shown on the persistent keyboard.
    public const string ButtonStats = "📊 Stats";
    public const string ButtonFind = "🔍 Find candidate";
    public const string ButtonHelp = "❓ Help";
    public const string ButtonLanguage = "🌐 Language";

    /// <summary>Telegram reply_markup for the persistent keyboard — two rows of two.</summary>
    public const string KeyboardJson =
        """{"keyboard":[[{"text":"📊 Stats"},{"text":"🔍 Find candidate"}],[{"text":"🌐 Language"},{"text":"❓ Help"}]],"resize_keyboard":true,"is_persistent":true}""";

    public static BotCommandParse Parse(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
            return new BotCommandParse(BotCommand.Unknown, string.Empty);

        // Keyboard buttons first — they are plain text, not commands.
        if (text == ButtonStats) return new BotCommandParse(BotCommand.Stats, string.Empty);
        if (text == ButtonHelp) return new BotCommandParse(BotCommand.Help, string.Empty);
        if (text == ButtonLanguage) return new BotCommandParse(BotCommand.Language, string.Empty);
        if (text == ButtonFind) return new BotCommandParse(BotCommand.Help, "find");

        if (!text.StartsWith('/'))
            return new BotCommandParse(BotCommand.Search, text);

        // Split "/cmd@botname arg" into verb + argument.
        var space = text.IndexOf(' ');
        var verb = (space < 0 ? text : text[..space]).ToLowerInvariant();
        var arg = space < 0 ? string.Empty : text[(space + 1)..].Trim();

        var at = verb.IndexOf('@');
        if (at > 0) verb = verb[..at];

        var command = verb switch
        {
            "/start" or "/register" => BotCommand.Start,
            "/help" or "/commands" or "/menu" => BotCommand.Help,
            "/link" => BotCommand.Link,
            "/lang" or "/language" => BotCommand.Language,
            "/status" or "/candidate" or "/find" or "/search" => BotCommand.Status,
            "/cv" => BotCommand.Cv,
            "/stats" or "/stat" or "/report" => BotCommand.Stats,
            _ => BotCommand.Unknown
        };

        return new BotCommandParse(command, arg);
    }

    /// <summary>
    /// True when a message is just a link code, so a bare paste links the chat instead of falling
    /// through to candidate search and answering "this chat is not linked yet" forever.
    /// </summary>
    public static bool LooksLikeLinkCode(string? raw) => BotLinkCodeRules.LooksLikeLinkCode(raw);

    public static string HelpText(bool amharic) => amharic
        ? "የሚከተሉትን መጠቀም ይችላሉ:\n\n"
          + "• የፓስፖርት ቁጥር ወይም ስም ብቻ ይላኩ — እጩውን እናገኛለን\n"
          + "• 📊 Stats — የዚህ ሳምንት/ወር/ዓመት ቁጥሮች\n"
          + "• /cv <ፓስፖርት> — ሲቪ ማውረድ\n"
          + "• /lang en — ወደ እንግሊዝኛ መቀየር"
        : "Here is what I can do:\n\n"
          + "• Just send a passport number or a name — I will find the candidate\n"
          + "• 📊 Stats — this week / month / year, or a stage e.g. \"stats embassy\"\n"
          + "• /cv <passport> — download the candidate's CV\n"
          + "• /lang am — switch to Amharic";

    public static string UnknownReply(bool amharic) => amharic
        ? "አላገኘሁትም። የፓስፖርት ቁጥር ወይም ስም ይላኩ፣ ወይም ❓ Help ይጫኑ።"
        : "I didn't understand that. Send a passport number or a name, or tap ❓ Help.";
}
