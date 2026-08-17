using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimbaFlow.Infrastructure.Options;

namespace SimbaFlow.Infrastructure.Services.Bot;

public sealed class TelegramPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly ITelegramGateway _telegram;
    private readonly ITelegramPollerState _state;
    private readonly ILogger<TelegramPollingService> _logger;

    public TelegramPollingService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TelegramOptions> options,
        ITelegramGateway telegram,
        ITelegramPollerState state,
        ILogger<TelegramPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _telegram = telegram;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            _state.IsConfigured = _telegram.IsConfigured;

            if (!_telegram.IsConfigured || !options.PollingEnabled)
            {
                _state.IsConnected = false;
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            try
            {
                var me = await _telegram.GetMeAsync(stoppingToken);
                _state.BotUsername = me?.Username;
                _state.IsConnected = me is not null;
                _state.LastConnectedAt = me is not null ? DateTime.UtcNow : _state.LastConnectedAt;
                _state.LastError = null;

                var updates = await _telegram.GetUpdatesAsync(
                    _state.LastUpdateId + 1,
                    options.LongPollTimeoutSeconds,
                    stoppingToken);

                foreach (var update in updates)
                {
                    // Handle each update in isolation. If one message throws, we must still
                    // advance LastUpdateId — otherwise Telegram keeps redelivering the same
                    // poisoned update and the bot is wedged offline for every user.
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var dispatcher = scope.ServiceProvider.GetRequiredService<ITelegramCommandDispatcher>();
                        await dispatcher.HandleAsync(update, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw; // shutting down
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to handle Telegram update {UpdateId} from chat {ChatId}",
                            update.UpdateId, update.ChatId);
                        try
                        {
                            await _telegram.SendMessageAsync(update.ChatId,
                                "Sorry — that command failed. Please try again or use the web app.",
                                stoppingToken);
                        }
                        catch
                        {
                            // Never let the apology failing take the loop down.
                        }
                    }
                    finally
                    {
                        _state.LastUpdateId = Math.Max(_state.LastUpdateId, update.UpdateId);
                    }
                }
            }
            catch (Exception ex)
            {
                _state.IsConnected = false;
                _state.LastError = ex.Message[..Math.Min(ex.Message.Length, 512)];
                _logger.LogWarning(ex, "Telegram polling loop failed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
