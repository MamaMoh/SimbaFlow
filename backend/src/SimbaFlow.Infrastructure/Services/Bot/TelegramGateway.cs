using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimbaFlow.Infrastructure.Options;

namespace SimbaFlow.Infrastructure.Services.Bot;

public interface ITelegramGateway
{
    bool IsConfigured { get; }
    Task<TelegramBotIdentity?> GetMeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct = default);
    Task<string?> SendMessageAsync(string chatId, string text, CancellationToken ct = default);
    /// <summary>Sends a message with a Telegram reply_markup payload (e.g. a persistent keyboard).</summary>
    Task<string?> SendMessageAsync(string chatId, string text, string? replyMarkupJson, CancellationToken ct = default);
    Task<string?> SendDocumentAsync(string chatId, byte[] content, string fileName, string caption, CancellationToken ct = default);
}

public sealed class TelegramGateway : ITelegramGateway
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly ILogger<TelegramGateway> _logger;

    public TelegramGateway(HttpClient httpClient, IOptionsMonitor<TelegramOptions> options, ILogger<TelegramGateway> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.CurrentValue.BotToken) && _options.CurrentValue.Enabled;

    public async Task<TelegramBotIdentity?> GetMeAsync(CancellationToken ct = default)
    {
        var root = await PostFormAsync("getMe", null, ct);
        if (root is null || !root.RootElement.GetProperty("ok").GetBoolean())
            return null;

        var result = root.RootElement.GetProperty("result");
        return new TelegramBotIdentity(
            result.GetProperty("id").GetInt64(),
            result.GetProperty("username").GetString() ?? string.Empty,
            result.GetProperty("first_name").GetString() ?? string.Empty);
    }

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["offset"] = offset.ToString(),
            ["timeout"] = timeoutSeconds.ToString()
        };

        var root = await PostFormAsync("getUpdates", form, ct);
        if (root is null || !root.RootElement.GetProperty("ok").GetBoolean())
            return [];

        var list = new List<TelegramUpdate>();
        foreach (var item in root.RootElement.GetProperty("result").EnumerateArray())
        {
            if (!item.TryGetProperty("update_id", out var updateIdProp))
                continue;

            var updateId = updateIdProp.GetInt64();
            if (!item.TryGetProperty("message", out var message))
                continue;

            if (!message.TryGetProperty("chat", out var chat) || !chat.TryGetProperty("id", out var chatIdProp))
                continue;

            var chatId = chatIdProp.GetInt64().ToString();
            var text = message.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
            var fromName = message.TryGetProperty("from", out var fromProp) && fromProp.TryGetProperty("username", out var userProp)
                ? userProp.GetString()
                : null;

            list.Add(new TelegramUpdate(updateId, chatId, text, fromName));
        }

        return list;
    }

    public Task<string?> SendMessageAsync(string chatId, string text, CancellationToken ct = default)
        => SendMessageAsync(chatId, text, null, ct);

    public async Task<string?> SendMessageAsync(string chatId, string text, string? replyMarkupJson, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["chat_id"] = chatId,
            ["text"] = text
        };
        if (!string.IsNullOrWhiteSpace(replyMarkupJson))
            form["reply_markup"] = replyMarkupJson;
        var root = await PostFormAsync("sendMessage", form, ct);
        return ExtractMessageId(root);
    }

    public async Task<string?> SendDocumentAsync(string chatId, byte[] content, string fileName, string caption, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return null;

        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(chatId), "chat_id");
            form.Add(new StringContent(caption), "caption");
            form.Add(new ByteArrayContent(content), "document", fileName);

            using var response = await _httpClient.PostAsync(BuildUrl("sendDocument"), form, ct);
            var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return ExtractMessageId(root);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram sendDocument failed");
            return null;
        }
    }

    private async Task<JsonDocument?> PostFormAsync(string method, Dictionary<string, string>? values, CancellationToken ct)
    {
        if (!IsConfigured)
            return null;

        try
        {
            using var content = values is null ? null : new FormUrlEncodedContent(values);
            using var response = await _httpClient.PostAsync(BuildUrl(method), content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return JsonDocument.Parse(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram call {Method} failed", method);
            return null;
        }
    }

    private string BuildUrl(string method) =>
        $"https://api.telegram.org/bot{_options.CurrentValue.BotToken}/{method}";

    private static string? ExtractMessageId(JsonDocument? root)
    {
        if (root is null || !root.RootElement.TryGetProperty("ok", out var okProp) || !okProp.GetBoolean())
            return null;
        return root.RootElement.TryGetProperty("result", out var result)
            && result.TryGetProperty("message_id", out var messageId)
            ? messageId.GetInt32().ToString()
            : null;
    }
}

public sealed record TelegramBotIdentity(long Id, string Username, string FirstName);
public sealed record TelegramUpdate(long UpdateId, string ChatId, string? Text, string? Username);
