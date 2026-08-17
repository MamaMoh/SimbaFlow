namespace SimbaFlow.Infrastructure.Services.Bot;

public interface ITelegramPollerState
{
    bool IsConfigured { get; set; }
    bool IsConnected { get; set; }
    string? BotUsername { get; set; }
    string? LastError { get; set; }
    DateTime? LastConnectedAt { get; set; }
    long LastUpdateId { get; set; }
}

public sealed class TelegramPollerState : ITelegramPollerState
{
    public bool IsConfigured { get; set; }
    public bool IsConnected { get; set; }
    public string? BotUsername { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public long LastUpdateId { get; set; }
}
