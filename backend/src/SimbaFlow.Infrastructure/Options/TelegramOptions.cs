namespace SimbaFlow.Infrastructure.Options;

/// <summary>
/// Telegram bot configuration sourced from appsettings/environment.
/// </summary>
public sealed class TelegramOptions
{
    public string BotToken { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool PollingEnabled { get; set; } = true;
    public int LongPollTimeoutSeconds { get; set; } = 25;
    public string BotUsername { get; set; } = string.Empty;
}
