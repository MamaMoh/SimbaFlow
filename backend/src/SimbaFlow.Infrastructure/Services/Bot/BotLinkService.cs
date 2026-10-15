using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.Infrastructure.Services.Bot;

public interface IBotLinkService
{
    Task<Result<LinkCodeDto>> CreateLinkCodeAsync(Guid userId, CancellationToken ct = default);

    Task<Result> ConsumeLinkCodeAsync(
        string chatId,
        string code,
        string? telegramUsername = null,
        string? displayName = null,
        CancellationToken ct = default);

    /// <summary>The account this chat speaks for, or null when the chat is not linked.</summary>
    Task<Guid?> ResolveUserIdAsync(string chatId, CancellationToken ct = default);

    /// <summary>Disconnects this one chat, leaving the account's other chats alone.</summary>
    Task<Result> UnlinkChatAsync(string chatId, CancellationToken ct = default);

    /// <summary>Disconnects every chat on this account.</summary>
    Task<Result> UnlinkCurrentUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>One of my own chats, by id. Refuses a link belonging to anybody else.</summary>
    Task<Result> RemoveOwnLinkAsync(Guid userId, Guid linkId, CancellationToken ct = default);

    Task<Result<List<BotChatLinkDto>>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Everyone in the agency who can be reached on Telegram, for the owner.</summary>
    Task<Result<List<AgencyBotChatLinkDto>>> ListForAgencyAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>The owner disconnecting somebody else's chat. Scoped to their own agency.</summary>
    Task<Result<AgencyBotChatLinkDto>> RemoveAgencyLinkAsync(
        Guid tenantId, Guid linkId, CancellationToken ct = default);

    /// <summary>Records that this chat is still in use. Cheap, and skipped when already fresh.</summary>
    Task TouchAsync(string chatId, string? telegramUsername, string? displayName, CancellationToken ct = default);

    /// <summary>Whether this account currently wants the bot's messages.</summary>
    Task<Result<bool>> GetNotificationsOnAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Turns this account's messages on or off. The same switch /mute and /unmute throw in the
    /// chat, so the setting reads the same on both.
    /// </summary>
    Task<Result<bool>> SetNotificationsOnAsync(Guid userId, bool on, CancellationToken ct = default);
}

public sealed class BotLinkService : IBotLinkService
{
    private readonly IPlatformDbContext _platform;
    private readonly IMemoryCache _attempts;
    private readonly ILogger<BotLinkService> _logger;

    public BotLinkService(
        IPlatformDbContext platform,
        IMemoryCache attempts,
        ILogger<BotLinkService> logger)
    {
        _platform = platform;
        _attempts = attempts;
        _logger = logger;
    }

    private static string ChatKey(string chatId) => $"botlink:attempts:{chatId}";

    private bool IsThrottled(string chatId, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (!_attempts.TryGetValue<AttemptState>(ChatKey(chatId), out var state) || state is null)
            return false;

        if (state.Count < BotLinkCodeRules.MaxAttemptsPerChat)
            return false;

        var elapsed = DateTime.UtcNow - state.WindowStart;
        if (elapsed >= BotLinkCodeRules.AttemptWindow)
        {
            _attempts.Remove(ChatKey(chatId));
            return false;
        }

        retryAfter = BotLinkCodeRules.AttemptWindow - elapsed;
        return true;
    }

    private void RecordFailure(string chatId)
    {
        var key = ChatKey(chatId);
        var state = _attempts.TryGetValue<AttemptState>(key, out var s) && s is not null
            ? s
            : new AttemptState { WindowStart = DateTime.UtcNow, Count = 0 };

        if (DateTime.UtcNow - state.WindowStart >= BotLinkCodeRules.AttemptWindow)
        {
            state.WindowStart = DateTime.UtcNow;
            state.Count = 0;
        }

        state.Count++;
        _attempts.Set(key, state, BotLinkCodeRules.AttemptWindow);
    }

    private sealed class AttemptState
    {
        public DateTime WindowStart { get; set; }
        public int Count { get; set; }
    }

    public async Task<Result<LinkCodeDto>> CreateLinkCodeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _platform.ApplicationUsers
            .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, ct);
        if (user is null)
            return Result<LinkCodeDto>.Failure("User not found", 404);

        // Generating a new code must retire the old ones, otherwise every code this user ever
        // generated stays valid for its full 10 minutes and "generate a new code" quietly widens
        // the window instead of replacing it.
        var now = DateTime.UtcNow;
        var superseded = await _platform.BotRegistrationChallenges
            .Where(x => x.UserId == userId && x.ConsumedAt == null && x.ExpiresAt > now && !x.IsDeleted)
            .ToListAsync(ct);
        foreach (var old in superseded)
            old.ExpiresAt = now;

        var code = BotLinkCodeRules.Generate();
        var challenge = new BotRegistrationChallenge
        {
            UserId = userId,
            Code = code,
            ExpiresAt = now.AddMinutes(10)
        };

        _platform.BotRegistrationChallenges.Add(challenge);
        await _platform.SaveChangesAsync(ct);

        return Result<LinkCodeDto>.Success(new LinkCodeDto(code, challenge.ExpiresAt));
    }

    public async Task<Result> ConsumeLinkCodeAsync(
        string chatId,
        string code,
        string? telegramUsername = null,
        string? displayName = null,
        CancellationToken ct = default)
    {
        // Throttle per chat. The code space alone makes guessing hopeless, but an attempt limit
        // stops the probing outright and leaves a trace in the logs.
        if (IsThrottled(chatId, out var retryAfter))
        {
            _logger.LogWarning("Bot link throttled for chat {ChatId}", chatId);
            return Result.Failure(
                $"Too many attempts. Try again in {PluralText.Count(retryAfter.Minutes + 1, "minute")}.", 429);
        }

        var normalized = BotLinkCodeRules.Normalize(code);
        var challenge = await _platform.BotRegistrationChallenges
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x =>
                x.Code == normalized &&
                x.ConsumedAt == null &&
                x.ExpiresAt > DateTime.UtcNow &&
                !x.IsDeleted, ct);

        if (challenge is null)
        {
            RecordFailure(chatId);
            return Result.Failure("That code is not valid any more. Generate a new one in the web app under Settings, then send it here.", 400);
        }

        var user = await _platform.ApplicationUsers
            .FirstOrDefaultAsync(x => x.Id == challenge.UserId && !x.IsDeleted, ct);
        if (user is null)
            return Result.Failure("User not found", 404);

        var existing = await _platform.BotChatLinks
            .FirstOrDefaultAsync(l => l.ChatId == chatId && !l.IsDeleted, ct);

        if (existing is not null && existing.UserId != user.Id)
        {
            // One chat cannot answer for two accounts, and moving it silently would redirect
            // somebody else's notifications without telling either of them. The agency's owner
            // can release it from the web application, which is the way out of this.
            var holder = await _platform.ApplicationUsers.AsNoTracking()
                .Where(u => u.Id == existing.UserId)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync(ct);
            _logger.LogWarning(
                "Bot link refused: chat already belongs to user {UserId}", existing.UserId);
            return Result.Failure(
                $"This Telegram account is already linked to {holder ?? "another account"}. "
                + "Disconnect it there first, or ask the agency owner to release it.", 409);
        }

        if (existing is null)
        {
            var already = await _platform.BotChatLinks
                .CountAsync(l => l.UserId == user.Id && !l.IsDeleted, ct);
            if (already >= BotLinkCodeRules.MaxChatsPerUser)
                return Result.Failure(
                    $"That account already has {PluralText.Count(already, "Telegram account")} linked, "
                    + "which is the most allowed. Disconnect one first.", 409);

            _platform.BotChatLinks.Add(new BotChatLink
            {
                UserId = user.Id,
                ChatId = chatId,
                TelegramUsername = Trim(telegramUsername, 64),
                DisplayName = Trim(displayName, 128),
                LinkedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            });
        }
        else
        {
            // Already theirs — re-sending a code is how somebody checks the link is still good.
            existing.TelegramUsername = Trim(telegramUsername, 64) ?? existing.TelegramUsername;
            existing.DisplayName = Trim(displayName, 128) ?? existing.DisplayName;
            existing.LastSeenAt = DateTime.UtcNow;
        }

        challenge.ConsumedAt = DateTime.UtcNow;
        await _platform.SaveChangesAsync(ct);

        _attempts.Remove(ChatKey(chatId));
        _logger.LogInformation("Linked Telegram chat for user {UserId}", user.Id);
        return Result.Success();
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }

    public async Task<Guid?> ResolveUserIdAsync(string chatId, CancellationToken ct = default) =>
        await _platform.BotChatLinks.AsNoTracking()
            .Where(l => l.ChatId == chatId && !l.IsDeleted)
            .Select(l => (Guid?)l.UserId)
            .FirstOrDefaultAsync(ct);

    public async Task<Result> UnlinkChatAsync(string chatId, CancellationToken ct = default)
    {
        var link = await _platform.BotChatLinks
            .FirstOrDefaultAsync(l => l.ChatId == chatId && !l.IsDeleted, ct);
        if (link is null)
            return Result.Failure("That chat is not linked.", 404);

        Forget(link);
        await _platform.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UnlinkCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _platform.ApplicationUsers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, ct);
        if (user is null)
            return Result.Failure("User not found", 404);

        var links = await _platform.BotChatLinks
            .Where(l => l.UserId == userId && !l.IsDeleted)
            .ToListAsync(ct);
        foreach (var link in links)
            Forget(link);

        await _platform.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveOwnLinkAsync(Guid userId, Guid linkId, CancellationToken ct = default)
    {
        var link = await _platform.BotChatLinks
            .FirstOrDefaultAsync(l => l.Id == linkId && !l.IsDeleted, ct);

        // Not found and not yours read the same way out here, so one account cannot probe for
        // another's link ids.
        if (link is null || link.UserId != userId)
            return Result.Failure("That link is not yours.", 404);

        Forget(link);
        await _platform.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<List<BotChatLinkDto>>> ListForUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        var rows = await _platform.BotChatLinks.AsNoTracking()
            .Where(l => l.UserId == userId && !l.IsDeleted)
            .OrderBy(l => l.LinkedAt)
            .Select(l => new BotChatLinkDto(
                l.Id, l.TelegramUsername, l.DisplayName, l.LinkedAt, l.LastSeenAt))
            .ToListAsync(ct);

        return Result<List<BotChatLinkDto>>.Success(rows);
    }

    public async Task<Result<bool>> GetNotificationsOnAsync(Guid userId, CancellationToken ct = default)
    {
        var muted = await _platform.ApplicationUsers.AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => (bool?)u.BotMuted)
            .FirstOrDefaultAsync(ct);

        return muted is null
            ? Result<bool>.Failure("User not found", 404)
            : Result<bool>.Success(!muted.Value);
    }

    public async Task<Result<bool>> SetNotificationsOnAsync(
        Guid userId, bool on, CancellationToken ct = default)
    {
        // Stored as "muted" because that is the word the chat command uses and the notifier
        // reads. The screen says it the other way round, because a switch labelled "muted" is
        // one people turn on expecting to be told things.
        var user = await _platform.ApplicationUsers
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null)
            return Result<bool>.Failure("User not found", 404);

        user.BotMuted = !on;
        await _platform.SaveChangesAsync(ct);
        return Result<bool>.Success(on);
    }

    public async Task<Result<List<AgencyBotChatLinkDto>>> ListForAgencyAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        // The chat id itself is deliberately not returned. The owner needs to know who is
        // reachable and be able to cut a link; nothing on the screen needs Telegram's id, and
        // handing it out would let it be pasted into somewhere it does not belong.
        var rows = await
            (from link in _platform.BotChatLinks.AsNoTracking().Where(l => !l.IsDeleted)
             join user in _platform.ApplicationUsers.AsNoTracking()
                 .Where(u => u.TenantId == tenantId && !u.IsDeleted)
                 on link.UserId equals user.Id
             orderby user.FirstName, user.LastName, link.LinkedAt
             select new AgencyBotChatLinkDto(
                 link.Id,
                 user.Id,
                 user.UserName,
                 user.FirstName,
                 user.LastName,
                 user.IsActive,
                 user.BotMuted,
                 link.TelegramUsername,
                 link.DisplayName,
                 link.LinkedAt,
                 link.LastSeenAt))
            .ToListAsync(ct);

        return Result<List<AgencyBotChatLinkDto>>.Success(rows);
    }

    public async Task<Result<AgencyBotChatLinkDto>> RemoveAgencyLinkAsync(
        Guid tenantId, Guid linkId, CancellationToken ct = default)
    {
        var link = await _platform.BotChatLinks
            .FirstOrDefaultAsync(l => l.Id == linkId && !l.IsDeleted, ct);
        if (link is null)
            return Result<AgencyBotChatLinkDto>.Failure("That link no longer exists.", 404);

        var user = await _platform.ApplicationUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == link.UserId && !u.IsDeleted, ct);

        // An owner reaches their own agency's staff and nobody else's. Without this, a link id
        // from another agency would be removable by anybody holding one.
        if (user is null || user.TenantId != tenantId)
            return Result<AgencyBotChatLinkDto>.Failure("That link no longer exists.", 404);

        var removed = new AgencyBotChatLinkDto(
            link.Id, user.Id, user.UserName, user.FirstName, user.LastName, user.IsActive,
            user.BotMuted, link.TelegramUsername, link.DisplayName, link.LinkedAt, link.LastSeenAt);

        Forget(link);
        await _platform.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Agency owner removed Telegram link {LinkId} for user {UserId}", linkId, user.Id);

        return Result<AgencyBotChatLinkDto>.Success(removed);
    }

    public async Task TouchAsync(
        string chatId,
        string? telegramUsername,
        string? displayName,
        CancellationToken ct = default)
    {
        var link = await _platform.BotChatLinks
            .FirstOrDefaultAsync(l => l.ChatId == chatId && !l.IsDeleted, ct);
        if (link is null) return;

        var username = Trim(telegramUsername, 64);
        var name = Trim(displayName, 128);

        // Every message would otherwise be a write. An hour's resolution is plenty for a column
        // whose only reader is an owner scanning for links nobody uses.
        var stale = link.LastSeenAt is null || DateTime.UtcNow - link.LastSeenAt.Value > TimeSpan.FromHours(1);
        var renamed = (username is not null && username != link.TelegramUsername)
                      || (name is not null && name != link.DisplayName);
        if (!stale && !renamed) return;

        link.LastSeenAt = DateTime.UtcNow;
        if (username is not null) link.TelegramUsername = username;
        if (name is not null) link.DisplayName = name;
        await _platform.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Soft-deleted rather than deleted, because the unique index on the chat id is filtered to
    /// live rows: the same phone can be linked again afterwards, and the old row stays as the
    /// record that it once was.
    /// </summary>
    private static void Forget(BotChatLink link)
    {
        link.IsDeleted = true;
        link.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record LinkCodeDto(string Code, DateTime ExpiresAt);

/// <summary>One of my own linked chats, as the Settings card shows it.</summary>
public sealed record BotChatLinkDto(
    Guid Id,
    string? TelegramUsername,
    string? DisplayName,
    DateTime LinkedAt,
    DateTime? LastSeenAt);

/// <summary>A linked chat with the person it belongs to, for the owner's roster.</summary>
public sealed record AgencyBotChatLinkDto(
    Guid Id,
    Guid UserId,
    string? UserName,
    string? FirstName,
    string? LastName,
    bool IsActive,
    bool Muted,
    string? TelegramUsername,
    string? DisplayName,
    DateTime LinkedAt,
    DateTime? LastSeenAt);
