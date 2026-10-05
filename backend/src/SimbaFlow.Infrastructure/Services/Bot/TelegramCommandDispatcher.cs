using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
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
                    BotMessages.AskForLinkCode(false), ct);
                return;
            }

            var result = await _botLinkService.ConsumeLinkCodeAsync(update.ChatId, code, ct);
            if (!result.IsSuccess)
            {
                await _telegram.SendMessageAsync(update.ChatId,
                    BotMessages.Escape(result.Error ?? "That code did not work. Generate a new one in the web app."),
                    ct);
                return;
            }

            var linked = await _userManager.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TelegramChatId == update.ChatId && x.BotLinked && !x.IsDeleted, ct);
            var agencyName = linked?.TenantId is Guid linkedTenant
                ? await _platform.Tenants.AsNoTracking()
                    .Where(t => t.Id == linkedTenant).Select(t => t.Name).FirstOrDefaultAsync(ct)
                : null;

            await _telegram.SendMessageAsync(update.ChatId,
                BotMessages.LinkSucceeded(linked?.FullName ?? linked?.UserName ?? "there", agencyName,
                    string.Equals(linked?.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase)),
                BotCommandRules.KeyboardJson,
                ct);
            return;
        }

        if (parsed.Command == BotCommand.Start)
        {
            var known = await _userManager.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TelegramChatId == update.ChatId && x.BotLinked && !x.IsDeleted, ct);

            // Telling someone who is already linked to go and link is the bot not knowing who it
            // is talking to — /start is the button Telegram shows on every reopen.
            await _telegram.SendMessageAsync(update.ChatId,
                known is null
                    ? BotMessages.Welcome(false)
                    : BotMessages.AlreadyLinked(
                        known.FullName ?? known.UserName ?? "there",
                        string.Equals(known.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase)),
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
                    BotMessages.LanguageChoices(
                        string.Equals(user.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase)),
                    ct);
                return;
            }
            var resolved = BotNotificationRules.ResolveLanguage(requested, user.PreferredLanguage);
            if (!BotNotificationRules.IsValidLanguage(requested))
            {
                await _telegram.SendMessageAsync(update.ChatId,
                    BotMessages.LanguageChoices(
                        string.Equals(user.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase)),
                    ct);
                return;
            }

            var linkedUser = await _userManager.FindByIdAsync(user.Id.ToString());
            if (linkedUser is not null)
            {
                linkedUser.PreferredLanguage = resolved;
                await _platform.SaveChangesAsync(ct);
            }
            await _telegram.SendMessageAsync(update.ChatId,
                BotMessages.LanguageUpdated(resolved == "am"), BotCommandRules.KeyboardJson, ct);
            return;
        }

        if (parsed.Command == BotCommand.Help)
        {
            var helpAm = string.Equals(user.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase);
            await _telegram.SendMessageAsync(update.ChatId,
                // "🔍 Find candidate" arrives here carrying "find". It used to print the whole help
                // text, which answers a question nobody asked; it is a prompt, so prompt.
                parsed.Argument == "find" ? BotMessages.AskForCandidate(helpAm) : BotMessages.Help(helpAm),
                BotCommandRules.KeyboardJson,
                ct);
            return;
        }

        // A platform admin has no tenant of their own and still needs the bot to answer — the
        // web app already falls back to the default agency schema for them.
        if (!user.TenantId.HasValue && !user.IsSuperAdmin)
        {
            await _telegram.SendMessageAsync(update.ChatId,
                BotMessages.NoAgency(
                    string.Equals(user.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase)),
                ct);
            return;
        }

        var amLang = string.Equals(user.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase);

        // A bare passport number or name is treated as a lookup — staff type that by instinct.
        if (parsed.Command == BotCommand.Status || parsed.Command == BotCommand.Search)
        {
            var query = parsed.Argument;
            if (string.IsNullOrWhiteSpace(query))
            {
                await _telegram.SendMessageAsync(update.ChatId, BotMessages.AskForCandidate(amLang), ct);
                return;
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(user.TenantId, user.IsSuperAdmin, ct);
            var matches = await FindCandidatesAsync(tenantDb, query, ct);

            if (matches.Count == 0)
            {
                await _telegram.SendMessageAsync(update.ChatId, BotMessages.CandidateNotFound(query, amLang), ct);
                return;
            }

            if (matches.Count == 1)
            {
                var c = matches[0];
                await _telegram.SendMessageAsync(update.ChatId,
                    BotMessages.CandidateCard(
                        c.FullName, c.PassportNumber, c.CurrentStageName, c.Status.ToString(),
                        c.CountryOfTravel, amLang),
                    ct);
                return;
            }

            // Several people answer to that name. Showing the shortlist beats picking one for
            // them, which is how the wrong candidate's details leave the building.
            var shown = matches.Take(BotCandidateSearch.MaxChoices)
                .Select(c => (c.FullName, c.PassportNumber, (string?)c.CurrentStageName))
                .ToList();
            await _telegram.SendMessageAsync(update.ChatId,
                BotMessages.CandidateChoices(shown, matches.Count, amLang), ct);
            return;
        }

        if (parsed.Command == BotCommand.Cv)
        {
            var query = parsed.Argument;
            if (string.IsNullOrWhiteSpace(query))
            {
                // Was a lookup for PassportNumber == "", which answered "Candidate not found" and
                // left the user thinking the command was broken rather than incomplete.
                await _telegram.SendMessageAsync(update.ChatId, BotMessages.AskForCandidate(amLang), ct);
                return;
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(user.TenantId, user.IsSuperAdmin, ct);
            var found = await FindCandidatesAsync(tenantDb, query, ct);

            if (found.Count == 0)
            {
                await _telegram.SendMessageAsync(update.ChatId, BotMessages.CandidateNotFound(query, amLang), ct);
                return;
            }

            if (found.Count > 1)
            {
                var choices = found.Take(BotCandidateSearch.MaxChoices)
                    .Select(c => (c.FullName, c.PassportNumber, (string?)c.CurrentStageName))
                    .ToList();
                await _telegram.SendMessageAsync(update.ChatId,
                    BotMessages.CandidateChoices(choices, found.Count, amLang), ct);
                return;
            }

            var candidate = found[0];

            // A CV takes about a second to draw. Without this the chat sits silent and the user
            // presses the button again.
            await _telegram.SendMessageAsync(update.ChatId, BotMessages.CvBeingPrepared(amLang), ct);

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
                await _telegram.SendMessageAsync(update.ChatId, BotMessages.NoStatsPermission(am), ct);
                return;
            }

            var (period, stageQuery) = BotStatsRules.ParseArgument(parsed.Argument);

            await using var statsDb = await _tenantFactory.CreateAsync(user.TenantId, user.IsSuperAdmin, ct);
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
            await _telegram.SendMessageAsync(update.ChatId, BotMessages.WebAppOnly(amLang), ct);
            return;
        }

        await _telegram.SendMessageAsync(update.ChatId,
            BotMessages.NotUnderstood(amLang),
            BotCommandRules.KeyboardJson,
            ct);
        _logger.LogDebug("Unhandled telegram command from {ChatId}: {Text}", update.ChatId, text);
    }

    /// <summary>
    /// Finds the candidates a typed query could mean.
    ///
    /// A passport number is matched exactly and answers on its own. Anything else is treated as a
    /// name: every word has to appear somewhere in first/middle/last, in any order and in any
    /// case. The old predicate concatenated first and last only, so "ETENESH ACHALU TOLESA" — a
    /// perfectly ordinary Ethiopian name — matched nobody, and because Postgres LIKE is
    /// case-sensitive, neither did "etenesh".
    ///
    /// Matching runs in the database for the passport case and in memory for names, over the
    /// tenant's candidates only. Name matching cannot be expressed as a translatable predicate
    /// once the query has an arbitrary number of terms.
    /// </summary>
    private static async Task<List<Candidate>> FindCandidatesAsync(
        ITenantDbContext db, string query, CancellationToken ct)
    {
        var trimmed = query.Trim();

        if (BotCandidateSearch.LooksLikePassport(trimmed))
        {
            var byPassport = await db.Candidates.AsNoTracking()
                .Where(c => !c.IsDeleted && c.PassportNumber.ToLower() == trimmed.ToLower())
                .ToListAsync(ct);
            if (byPassport.Count > 0) return byPassport;
            // Not a passport we hold — fall through and try it as a name.
        }

        var terms = BotCandidateSearch.NameTerms(trimmed);
        if (terms.Count == 0) return [];

        // Narrow in the database on the first term, then apply the rest in memory. One term is
        // enough to cut the set to a handful even in an agency with thousands of candidates.
        var lead = terms[0];
        var shortlist = await db.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted &&
                (c.FirstName.ToLower().Contains(lead)
                 || c.LastName.ToLower().Contains(lead)
                 || (c.MiddleName != null && c.MiddleName.ToLower().Contains(lead))))
            .OrderBy(c => c.FirstName).ThenBy(c => c.LastName)
            .Take(200)
            .ToListAsync(ct);

        return shortlist
            .Where(c => BotCandidateSearch.NameMatches(
                c.FirstName, c.MiddleName, c.LastName, c.LocalFullName, trimmed))
            .ToList();
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
