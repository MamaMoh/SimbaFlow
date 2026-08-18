using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Services;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Infrastructure.Services.Bot;

public interface ITelegramCommandDispatcher
{
    Task HandleAsync(TelegramUpdate update, CancellationToken ct = default);
}

public sealed class TelegramCommandDispatcher : ITelegramCommandDispatcher
{
    private readonly IPlatformDbContext _platform;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantBotDbContextFactory _tenantFactory;
    private readonly ICvGenerationService _cvGenerationService;
    private readonly ITelegramGateway _telegram;
    private readonly IBotLinkService _botLinkService;
    private readonly ILogger<TelegramCommandDispatcher> _logger;

    public TelegramCommandDispatcher(
        IPlatformDbContext platform,
        UserManager<ApplicationUser> userManager,
        ITenantBotDbContextFactory tenantFactory,
        ICvGenerationService cvGenerationService,
        ITelegramGateway telegram,
        IBotLinkService botLinkService,
        ILogger<TelegramCommandDispatcher> logger)
    {
        _platform = platform;
        _userManager = userManager;
        _tenantFactory = tenantFactory;
        _cvGenerationService = cvGenerationService;
        _telegram = telegram;
        _botLinkService = botLinkService;
        _logger = logger;
    }

    public async Task HandleAsync(TelegramUpdate update, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(update.Text))
            return;

        var text = update.Text.Trim();
        var parsed = BotCommandRules.Parse(text);

        // "/link 123456" and a bare "123456" both mean the same thing to the person typing it.
        if (parsed.Command == BotCommand.Link || BotCommandRules.LooksLikeLinkCode(text))
        {
            var code = parsed.Command == BotCommand.Link ? parsed.Argument : text;
            if (string.IsNullOrWhiteSpace(code))
            {
                await _telegram.SendMessageAsync(update.ChatId,
                    "Send the 6-digit code from the web app, e.g. /link 123456", ct);
                return;
            }

            var result = await _botLinkService.ConsumeLinkCodeAsync(update.ChatId, code, ct);
            await _telegram.SendMessageAsync(update.ChatId,
                result.IsSuccess
                    ? "Linked. You can now send a passport number or a name to look someone up."
                    : result.Error ?? "Link failed.",
                result.IsSuccess ? BotCommandRules.KeyboardJson : null,
                ct);
            return;
        }

        if (parsed.Command == BotCommand.Start)
        {
            await _telegram.SendMessageAsync(update.ChatId,
                "Welcome to SimbaFlow.\n\n"
                + "To get started, open the web app, generate a link code under Settings, "
                + "then send it here as:\n/link CODE",
                BotCommandRules.KeyboardJson,
                ct);
            return;
        }

        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TelegramChatId == update.ChatId && x.BotLinked && !x.IsDeleted, ct);
        if (user is null)
        {
            await _telegram.SendMessageAsync(update.ChatId,
                BotNotificationRules.UnlinkedInstructionsReply(),
                ct);
            return;
        }

        if (parsed.Command == BotCommand.Language)
        {
            var requested = parsed.Argument;
            if (string.IsNullOrWhiteSpace(requested))
            {
                await _telegram.SendMessageAsync(update.ChatId,
                    "Send /lang en for English or /lang am for Amharic.", ct);
                return;
            }
            var resolved = BotNotificationRules.ResolveLanguage(requested, user.PreferredLanguage);
            if (!BotNotificationRules.IsValidLanguage(requested))
            {
                await _telegram.SendMessageAsync(update.ChatId, "Language must be 'en' or 'am'.", ct);
                return;
            }

            var linkedUser = await _userManager.FindByIdAsync(user.Id.ToString());
            if (linkedUser is not null)
            {
                linkedUser.PreferredLanguage = resolved;
                await _platform.SaveChangesAsync(ct);
            }
            await _telegram.SendMessageAsync(update.ChatId, resolved == "am" ? "ቋንቋ ተቀይሯል።" : "Language updated.", ct);
            return;
        }

        if (parsed.Command == BotCommand.Help)
        {
            await _telegram.SendMessageAsync(update.ChatId,
                BotCommandRules.HelpText(user.PreferredLanguage == "am"),
                BotCommandRules.KeyboardJson,
                ct);
            return;
        }

        if (!user.TenantId.HasValue)
        {
            await _telegram.SendMessageAsync(update.ChatId, "No tenant is linked to this user.", ct);
            return;
        }

        // A bare passport number or name is treated as a lookup — staff type that by instinct.
        if (parsed.Command == BotCommand.Status || parsed.Command == BotCommand.Search)
        {
            var query = parsed.Argument;
            if (string.IsNullOrWhiteSpace(query))
            {
                await _telegram.SendMessageAsync(update.ChatId,
                    user.PreferredLanguage == "am"
                        ? "የፓስፖርት ቁጥር ወይም ስም ይላኩ።"
                        : "Send a passport number or a name.", ct);
                return;
            }
            await using var tenantDb = await _tenantFactory.CreateAsync(user.TenantId.Value, ct);
            var candidate = await tenantDb.Candidates
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    !c.IsDeleted &&
                    (c.PassportNumber == query || (c.FirstName + " " + c.LastName).Contains(query)), ct);

            var reply = candidate is null
                ? (user.PreferredLanguage == "am"
                    ? "እጩ አልተገኘም።"
                    : "Candidate not found.")
                : (user.PreferredLanguage == "am"
                    ? $"እጩ: {candidate.FullName}\nደረጃ: {candidate.CurrentStageName ?? "Unknown"}\nሁኔታ: {candidate.Status}"
                    : $"Candidate: {candidate.FullName}\nStage: {candidate.CurrentStageName ?? "Unknown"}\nStatus: {candidate.Status}");

            await _telegram.SendMessageAsync(update.ChatId, reply, ct);
            return;
        }

        if (parsed.Command == BotCommand.Cv)
        {
            var passport = parsed.Argument;
            await using var tenantDb = await _tenantFactory.CreateAsync(user.TenantId.Value, ct);
            var candidate = await tenantDb.Candidates
                .AsNoTracking()
                .FirstOrDefaultAsync(c => !c.IsDeleted && c.PassportNumber == passport, ct);

            if (candidate is null)
            {
                await _telegram.SendMessageAsync(update.ChatId, "Candidate not found.", ct);
                return;
            }

            var pdf = await _cvGenerationService.GenerateAsync(candidate, cancellationToken: ct);
            await _telegram.SendDocumentAsync(update.ChatId, pdf, $"{candidate.PassportNumber}-cv.pdf", candidate.FullName, ct);
            return;
        }

        if (parsed.Command == BotCommand.Stats)
        {
            var am = user.PreferredLanguage == "am";

            // Agency-wide numbers are management information, so require a reporting permission.
            if (!await HasStatsPermissionAsync(user, ct))
            {
                await _telegram.SendMessageAsync(update.ChatId,
                    am
                        ? "ይህን መረጃ ለማየት ፈቃድ የለዎትም።"
                        : "You do not have permission to view agency statistics.",
                    ct);
                return;
            }

            var (period, stageQuery) = BotStatsRules.ParseArgument(parsed.Argument);

            await using var statsDb = await _tenantFactory.CreateAsync(user.TenantId.Value, ct);
            var active = statsDb.Candidates.AsNoTracking()
                .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

            string reply;

            if (stageQuery is not null)
            {
                // Stage-specific: total in that stage plus a breakdown of its tracks.
                var stage = await statsDb.WorkflowStages.AsNoTracking()
                    .FirstOrDefaultAsync(s => !s.IsDeleted && s.Name.ToLower() == stageQuery.ToLower(), ct)
                    ?? await statsDb.WorkflowStages.AsNoTracking()
                        .FirstOrDefaultAsync(s => !s.IsDeleted && s.Name.ToLower().Contains(stageQuery.ToLower()), ct);

                if (stage is null)
                {
                    var names = await statsDb.WorkflowStages.AsNoTracking()
                        .Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder)
                        .Select(s => s.Name).ToListAsync(ct);
                    await _telegram.SendMessageAsync(update.ChatId,
                        (am ? "ደረጃ አልተገኘም። ያሉት: " : "Stage not found. Available: ") + string.Join(", ", names),
                        ct);
                    return;
                }

                var inStage = await active.CountAsync(c => c.CurrentStageId == stage.Id, ct);

                // Mirror visibility lives in a Guid[] column that EF cannot translate inside an
                // aggregate, so project the two columns and count in memory (same approach the
                // Embassy/LMIS board queries use).
                var visibility = await active
                    .Where(c => c.CurrentStageId != stage.Id)
                    .Select(c => c.VisibleInStages)
                    .ToListAsync(ct);
                var mirrored = visibility.Count(v => v != null && v.Contains(stage.Id));

                var lines = new List<string>
                {
                    am ? $"📊 {stage.Name} — {inStage} እጩ" : $"📊 {stage.Name} — {inStage} candidate(s)",
                };
                if (mirrored > 0)
                    lines.Add(am ? $"(+{mirrored} በማንጸባረቅ)" : $"(+{mirrored} mirrored in)");

                // Track breakdown (e.g. Medical / Tasheer / Visa on Embassy).
                var tracks = await statsDb.WorkflowStageStatuses.AsNoTracking()
                    .Where(s => s.WorkflowStageId == stage.Id && s.TrackName != null)
                    .Select(s => s.TrackName!)
                    .Distinct()
                    .ToListAsync(ct);

                if (tracks.Count > 0 && inStage > 0)
                {
                    var rows = await active
                        .Where(c => c.CurrentStageId == stage.Id && c.CurrentStatusValues != null)
                        .Select(c => c.CurrentStatusValues)
                        .ToListAsync(ct);

                    foreach (var track in tracks.OrderBy(t => t))
                    {
                        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        foreach (var doc in rows)
                        {
                            if (doc is null) continue;
                            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
                            if (!doc.RootElement.TryGetProperty(track, out var el)) continue;
                            if (el.ValueKind != System.Text.Json.JsonValueKind.String) continue;
                            var v = el.GetString();
                            if (string.IsNullOrWhiteSpace(v)) continue;
                            counts[v] = counts.GetValueOrDefault(v) + 1;
                        }
                        if (counts.Count == 0) continue;
                        var parts = counts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}");
                        lines.Add($"• {track}: {string.Join(", ", parts)}");
                    }
                }

                reply = string.Join("\n", lines);
            }
            else
            {
                // Period summary: registrations in the window + the whole pipeline by stage.
                var p = period ?? StatsPeriod.AllTime;
                var from = BotStatsRules.StartOf(p, DateTime.UtcNow);
                var registered = from is null
                    ? await active.CountAsync(ct)
                    : await active.CountAsync(c => c.RegisteredAt >= from, ct);

                var stages = await statsDb.WorkflowStages.AsNoTracking()
                    .Where(s => !s.IsDeleted)
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new { s.Id, s.Name })
                    .ToListAsync(ct);

                var perStage = await active
                    .GroupBy(c => c.CurrentStageId)
                    .Select(g => new { StageId = g.Key, Count = g.Count() })
                    .ToListAsync(ct);
                var byStage = perStage.Where(x => x.StageId != null)
                    .ToDictionary(x => x.StageId!.Value, x => x.Count);

                var total = await active.CountAsync(ct);
                var header = am
                    ? $"📊 {BotStatsRules.PeriodLabel(p, true)}: {registered} አዲስ ምዝገባ\nጠቅላላ ንቁ እጩ: {total}"
                    : $"📊 {BotStatsRules.PeriodLabel(p, false)}: {registered} new registration(s)\nTotal active: {total}";

                var stageLines = stages
                    .Select(s => $"• {s.Name}: {byStage.GetValueOrDefault(s.Id, 0)}")
                    .ToList();

                reply = header + "\n\n" + (am ? "በደረጃ:" : "By stage:") + "\n" + string.Join("\n", stageLines)
                    + "\n\n" + (am
                        ? "ተጨማሪ: /stats week | month | year | <ደረጃ>"
                        : "More: /stats week | month | year | <stage>");
            }

            await _telegram.SendMessageAsync(update.ChatId, reply, ct);
            return;
        }

        if (text.StartsWith("/medical", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("/arrived", StringComparison.OrdinalIgnoreCase))
        {
            await _telegram.SendMessageAsync(update.ChatId,
                user.PreferredLanguage == "am"
                    ? "ይህ ከድር መተግበሪያው ይከናወናል።"
                    : "Please do this from the web app.",
                ct);
            return;
        }

        await _telegram.SendMessageAsync(update.ChatId,
            BotCommandRules.UnknownReply(user.PreferredLanguage == "am"),
            BotCommandRules.KeyboardJson,
            ct);
        _logger.LogDebug("Unhandled telegram command from {ChatId}: {Text}", update.ChatId, text);
    }

    /// <summary>
    /// Agency-wide statistics are management information, so the linked staff member must hold a
    /// reporting permission through one of their roles (SuperAdmin always passes).
    /// </summary>
    private async Task<bool> HasStatsPermissionAsync(ApplicationUser user, CancellationToken ct)
    {
        if (user.IsSuperAdmin) return true;

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Count == 0) return false;

        return await _platform.RolePermissions
            .AsNoTracking()
            .Include(rp => rp.Permission)
            .Include(rp => rp.Role)
            .AnyAsync(rp =>
                roles.Contains(rp.Role.Name!) &&
                BotStatsRules.AllowedPermissions.Contains(rp.Permission.Code), ct);
    }
}
