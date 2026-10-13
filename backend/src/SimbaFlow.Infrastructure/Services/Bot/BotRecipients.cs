using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Services.Bot;

/// <summary>
/// Who in an agency the bot can reach, and on which chats.
///
/// Three services send to staff — stage pushes, candidate events and the morning digest — and
/// each of them used to read one chat id off the account. With several chats per account the
/// query is a join and the send is a loop, and all three have to agree about that or one of them
/// goes on writing to a single phone.
/// </summary>
public static class BotRecipients
{
    /// <summary>One member of staff and every Telegram chat they have linked.</summary>
    public sealed record Recipient(
        Guid UserId,
        string? UserName,
        string? PreferredLanguage,
        bool Muted,
        string? WatchStages,
        List<string> ChatIds);

    public static async Task<List<Recipient>> ForTenantAsync(
        IPlatformDbContext platform,
        Guid tenantId,
        CancellationToken ct)
    {
        var rows = await
            (from link in platform.BotChatLinks.AsNoTracking().Where(l => !l.IsDeleted)
             join user in platform.ApplicationUsers.AsNoTracking()
                 .Where(u => u.TenantId == tenantId && !u.IsDeleted)
                 on link.UserId equals user.Id
             select new
             {
                 user.Id,
                 user.UserName,
                 user.PreferredLanguage,
                 user.BotMuted,
                 user.BotWatchStages,
                 link.ChatId,
             })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.Id)
            .Select(g => new Recipient(
                g.Key,
                g.First().UserName,
                g.First().PreferredLanguage,
                g.First().BotMuted,
                g.First().BotWatchStages,
                g.Select(r => r.ChatId).Distinct().ToList()))
            .ToList();
    }

    /// <summary>Agencies with at least one linked chat — the only ones a digest is worth building for.</summary>
    public static async Task<List<Guid>> TenantsWithListenersAsync(
        IPlatformDbContext platform,
        CancellationToken ct) =>
        await (from link in platform.BotChatLinks.AsNoTracking().Where(l => !l.IsDeleted)
               join user in platform.ApplicationUsers.AsNoTracking()
                   .Where(u => !u.IsDeleted && u.TenantId != null)
                   on link.UserId equals user.Id
               select user.TenantId!.Value)
            .Distinct()
            .ToListAsync(ct);
}
