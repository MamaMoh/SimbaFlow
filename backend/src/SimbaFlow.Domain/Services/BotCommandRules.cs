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
    /// <summary>Who is standing in a stage, by name.</summary>
    Stage,
    /// <summary>Candidates sitting in one stage longer than they should be.</summary>
    Overdue,
    /// <summary>Passports running out.</summary>
    Expiring,
    /// <summary>The caller's own queue.</summary>
    Me,
    /// <summary>Disconnect this chat from the account.</summary>
    Unlink,
    /// <summary>Stop the notifications without disconnecting.</summary>
    Mute,
    /// <summary>Start them again.</summary>
    Unmute,
    /// <summary>Only hear about certain stages.</summary>
    Watch,
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
    public const string ButtonStage = "📋 Stage list";
    public const string ButtonOverdue = "⏰ Overdue";
    public const string ButtonExpiring = "🛂 Expiring";
    public const string ButtonMyWork = "🗂 My work";

    /// <summary>
    /// The persistent keyboard: everything that can be done in one tap.
    ///
    /// Not every command can be a button. A button sends a fixed string, so anything that needs
    /// an argument — a name, a passport, a stage — cannot be one press. Those buttons ask the
    /// question instead and read the answer out of the next message, which is how a thumb gets
    /// through the same work as a keyboard.
    ///
    /// /unlink is deliberately not here. It is the one destructive thing the bot does, and a
    /// thumb-width button next to Help is how it gets pressed by accident.
    /// </summary>
    public const string KeyboardJson =
        """{"keyboard":[[{"text":"🔍 Find candidate"},{"text":"📊 Stats"}],[{"text":"📋 Stage list"},{"text":"⏰ Overdue"}],[{"text":"🛂 Expiring"},{"text":"🗂 My work"}],[{"text":"🌐 Language"},{"text":"❓ Help"}]],"resize_keyboard":true,"is_persistent":true}""";

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
        if (text == ButtonStage) return new BotCommandParse(BotCommand.Stage, string.Empty);
        if (text == ButtonOverdue) return new BotCommandParse(BotCommand.Overdue, string.Empty);
        if (text == ButtonExpiring) return new BotCommandParse(BotCommand.Expiring, string.Empty);
        if (text == ButtonMyWork) return new BotCommandParse(BotCommand.Me, string.Empty);

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
            "/stage" or "/list" or "/board" => BotCommand.Stage,
            "/overdue" or "/stuck" => BotCommand.Overdue,
            "/expiring" or "/expiry" => BotCommand.Expiring,
            "/me" or "/mywork" or "/today" => BotCommand.Me,
            "/unlink" or "/disconnect" => BotCommand.Unlink,
            "/mute" or "/quiet" => BotCommand.Mute,
            "/unmute" => BotCommand.Unmute,
            "/watch" or "/follow" => BotCommand.Watch,
            _ => BotCommand.Unknown
        };

        return new BotCommandParse(command, arg);
    }

    /// <summary>
    /// True when a message is just a link code, so a bare paste links the chat instead of falling
    /// through to candidate search and answering "this chat is not linked yet" forever.
    /// </summary>
    public static bool LooksLikeLinkCode(string? raw) => BotLinkCodeRules.LooksLikeLinkCode(raw);

    /// <summary>
    /// The menu Telegram shows when someone types "/". Registered at startup via setMyCommands.
    /// Descriptions are deliberately short — Telegram truncates them in a narrow list.
    /// </summary>
    public const string MyCommandsJson =
        """
        [
          {"command":"find","description":"Find a candidate by passport or name"},
          {"command":"cv","description":"Get a candidate's CV as a PDF"},
          {"command":"stats","description":"Where the agency stands"},
          {"command":"stage","description":"Who is standing in a stage"},
          {"command":"overdue","description":"Candidates stuck too long in one stage"},
          {"command":"expiring","description":"Passports running out"},
          {"command":"me","description":"Your own queue"},
          {"command":"mute","description":"Stop notifications without disconnecting"},
          {"command":"unmute","description":"Start notifications again"},
          {"command":"watch","description":"Only hear about certain stages"},
          {"command":"lang","description":"Switch between English and Amharic"},
          {"command":"help","description":"What this bot can do"},
          {"command":"unlink","description":"Disconnect this chat from your account"}
        ]
        """;

}
