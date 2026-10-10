using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Services.Bot;

/// <summary>
/// Whether an agency has turned the Telegram bot off.
///
/// Asked in three places — before answering a command, before a candidate event goes out, and
/// before a stage change does — so it lives here rather than being written three times and
/// drifting. Reads nothing but one flag, and treats an agency it cannot find as switched on:
/// a lookup that fails should not silence a bot that was working.
/// </summary>
public static class BotSwitch
{
    public static async Task<bool> IsOffAsync(
        IPlatformDbContext platform, Guid? tenantId, CancellationToken ct = default)
    {
        if (tenantId is not Guid id) return false;

        var settings = await platform.Tenants
            .AsNoTracking()
            .Where(t => t.Id == id && !t.IsDeleted)
            .Select(t => t.Settings)
            .FirstOrDefaultAsync(ct);

        return settings?.BotDisabled == true;
    }
}
