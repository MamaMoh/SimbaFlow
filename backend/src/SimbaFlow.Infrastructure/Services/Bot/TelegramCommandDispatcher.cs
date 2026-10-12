using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Documents;
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
    private readonly IFileStorageService _fileStorage;
    private readonly IMemoryCache _cache;
    private readonly ITelegramGateway _telegram;
    private readonly IBotLinkService _botLinkService;
    private readonly ILogger<TelegramCommandDispatcher> _logger;

    public TelegramCommandDispatcher(
        IPlatformDbContext platform,
        UserManager<ApplicationUser> userManager,
        ITenantBotDbContextFactory tenantFactory,
        ICvGenerationService cvGenerationService,
        IFileStorageService fileStorage,
        IMemoryCache cache,
        ITelegramGateway telegram,
        IBotLinkService botLinkService,
        ILogger<TelegramCommandDispatcher> logger)
    {
        _platform = platform;
        _userManager = userManager;
        _tenantFactory = tenantFactory;
        _cvGenerationService = cvGenerationService;
        _fileStorage = fileStorage;
        _cache = cache;
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

        // The agency's own off switch. /unlink still answers: somebody whose agency has turned
        // the bot off should still be able to disconnect their chat rather than be stuck.
        if (parsed.Command != BotCommand.Unlink && await BotSwitch.IsOffAsync(_platform, user.TenantId, ct))
        {
            await _telegram.SendMessageAsync(update.ChatId,
                BotMessages.BotTurnedOff(
                    string.Equals(user.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase)),
                ct);
            return;
        }

        if (parsed.Command == BotCommand.Unlink)
        {
            var unlinkAm = string.Equals(user.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase);
            // Confirmed in words rather than on one tap: losing the link means losing every
            // notification until somebody goes back to the web application for a new code.
            if (!parsed.Argument.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                await _telegram.SendMessageAsync(update.ChatId, BotMessages.UnlinkConfirm(unlinkAm), ct);
                return;
            }

            await _botLinkService.UnlinkCurrentUserAsync(user.Id, ct);
            await _telegram.SendMessageAsync(update.ChatId, BotMessages.Unlinked(unlinkAm), ct);
            return;
        }

        // A button asked a question a moment ago, and this is the answer to it. Only bare text
        // is treated this way — typing a real command instead abandons the question.
        if (parsed.Command == BotCommand.Search)
        {
            if (TakePendingPrompt(update.ChatId) is BotCommand pending && pending != BotCommand.Search)
                parsed = new BotCommandParse(pending, parsed.Argument);
        }
        else
        {
            _cache.Remove(PromptKey(update.ChatId));
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
                var card = BotMessages.CandidateCard(
                    c.FullName, c.PassportNumber, c.CurrentStageName, c.Status.ToString(),
                    c.CountryOfTravel, amLang);

                // The photograph with the card under it. A name and a passport number is not
                // enough to know you have the right person, and checking used to mean leaving
                // the chat for the web application.
                var (photo, _) = await CandidatePhotos.LoadAsync(_fileStorage, c, ct);
                if (photo is not null &&
                    await _telegram.SendPhotoAsync(
                        update.ChatId, photo, $"{c.PassportNumber}.jpg", card, ct) is not null)
                {
                    return;
                }

                // No photograph on file, or Telegram would not take it — the card still goes.
                await _telegram.SendMessageAsync(update.ChatId, card, ct);
                return;
            }

            // Several people answer to that name, which is what someone typing a first name is
            // asking for. Send all of them rather than a sample.
            await SendCandidateListAsync(update.ChatId, matches, query, amLang, ct);
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
                // A CV is one document, so the bot cannot guess which of them you meant.
                await SendCandidateListAsync(update.ChatId, found, query, amLang, ct);
                return;
            }

            var candidate = found[0];

            // Everything the renderer needs has to be handed to it here. There is no signed-in
            // user behind a Telegram message, so the layout and the letterhead cannot be read
            // from the request the way they are in the browser: the bot sent every CV on the
            // default form, with no letterhead and empty photo frames, while the same candidate
            // printed correctly from the web application.
            var template = await tenantDb.AgencyIntakeDefaults
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .Select(x => x.CvTemplate)
                .FirstOrDefaultAsync(ct);

            var (photo, fullPhoto) = await CandidatePhotos.LoadAsync(_fileStorage, candidate, ct);

            var pdf = await _cvGenerationService.GenerateForTenantAsync(
                candidate, user.TenantId, template, photo, fullPhoto, ct);

            // The photograph with the card on it, the same as a name or passport lookup gives,
            // then the file. Nothing is said twice: the card leaves out the "ask me for the CV"
            // line, since the CV is the next thing in the chat, and the file carries a caption
            // only when there was no photograph to put the name on.
            var sentPhoto = photo is not null
                && await _telegram.SendPhotoAsync(
                    update.ChatId,
                    photo,
                    $"{candidate.PassportNumber}.jpg",
                    BotMessages.CandidateCard(
                        candidate.FullName, candidate.PassportNumber, candidate.CurrentStageName,
                        candidate.Status.ToString(), candidate.CountryOfTravel, amLang,
                        withCvHint: false),
                    ct) is not null;

            await _telegram.SendDocumentAsync(
                update.ChatId,
                pdf,
                $"{candidate.PassportNumber}-cv.pdf",
                sentPhoto ? "" : candidate.FullName,
                ct);
            return;
        }

        if (parsed.Command is BotCommand.Mute or BotCommand.Unmute)
        {
            var linked = await _userManager.FindByIdAsync(user.Id.ToString());
            if (linked is not null)
            {
                linked.BotMuted = parsed.Command == BotCommand.Mute;
                await _platform.SaveChangesAsync(ct);
            }
            await _telegram.SendMessageAsync(update.ChatId,
                parsed.Command == BotCommand.Mute
                    ? BotMessages.Muted(amLang)
                    : BotMessages.Unmuted(amLang),
                ct);
            return;
        }

        if (parsed.Command == BotCommand.Watch)
        {
            var linked = await _userManager.FindByIdAsync(user.Id.ToString());
            if (linked is not null && !string.IsNullOrWhiteSpace(parsed.Argument))
            {
                // "all" is how you undo it, and an empty list is what "all" is stored as.
                linked.BotWatchStages = BotSubscription.MeansEverything(parsed.Argument)
                    ? null
                    : BotSubscription.Format(parsed.Argument.Split(','));
                await _platform.SaveChangesAsync(ct);
            }

            await _telegram.SendMessageAsync(update.ChatId,
                BotMessages.Watching(
                    BotSubscription.Parse(linked?.BotWatchStages ?? user.BotWatchStages), amLang),
                ct);
            return;
        }

        if (parsed.Command == BotCommand.Stage)
        {
            await using var stageDb = await _tenantFactory.CreateAsync(user.TenantId, user.IsSuperAdmin, ct);
            var stageNames = await stageDb.WorkflowStages.AsNoTracking()
                .Where(x => !x.IsDeleted).OrderBy(x => x.SortOrder)
                .Select(x => x.Name).ToListAsync(ct);

            if (string.IsNullOrWhiteSpace(parsed.Argument))
            {
                // The button sends no argument, so ask — and remember that we asked, so the
                // next bare message is read as the stage name rather than as a candidate search.
                AskFor(update.ChatId, BotCommand.Stage);
                await _telegram.SendMessageAsync(update.ChatId,
                    BotMessages.AskForStage(stageNames, amLang), ct);
                return;
            }

            var wanted = parsed.Argument.Trim();
            var stage = await stageDb.WorkflowStages.AsNoTracking()
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.Name.ToLower() == wanted.ToLower(), ct)
                ?? await stageDb.WorkflowStages.AsNoTracking()
                    .FirstOrDefaultAsync(x => !x.IsDeleted && x.Name.ToLower().Contains(wanted.ToLower()), ct);

            if (stage is null)
            {
                await _telegram.SendMessageAsync(update.ChatId,
                    BotMessages.AskForStage(stageNames, amLang), ct);
                return;
            }

            var now = DateTime.UtcNow;
            var standing = await stageDb.Candidates.AsNoTracking()
                .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active && c.CurrentStageId == stage.Id)
                .OrderBy(c => c.StageEnteredAt)
                .Select(c => new { c.FirstName, c.MiddleName, c.LastName, c.PassportNumber, c.StageEnteredAt })
                .Take(BotCandidateSearch.MaxResults)
                .ToListAsync(ct);

            var rows = standing
                .Select(c => (
                    FullName: string.Join(" ", new[] { c.FirstName, c.MiddleName, c.LastName }
                        .Where(n => !string.IsNullOrWhiteSpace(n))),
                    PassportNumber: (string?)c.PassportNumber,
                    // The heading is the stage, so each line only has to say how long.
                    Detail: (string?)BotMessages.DaysHere(
                        WorkQueueRules.DaysInStage(c.StageEnteredAt, now), amLang)))
                .ToList();

            foreach (var page in BotMessages.PeopleList(stage.Name, rows, null, amLang))
                await _telegram.SendMessageAsync(update.ChatId, page, ct);
            return;
        }

        if (parsed.Command == BotCommand.Overdue)
        {
            await using var db = await _tenantFactory.CreateAsync(user.TenantId, user.IsSuperAdmin, ct);
            var now = DateTime.UtcNow;
            var cutoff = WorkQueueRules.OverdueCutoff(now);

            var stuck = await db.Candidates.AsNoTracking()
                .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active
                            && c.StageEnteredAt != null && c.StageEnteredAt < cutoff)
                .OrderBy(c => c.StageEnteredAt)
                .Select(c => new { c.FirstName, c.MiddleName, c.LastName, c.PassportNumber,
                                   c.CurrentStageName, c.StageEnteredAt })
                .Take(BotCandidateSearch.MaxResults)
                .ToListAsync(ct);

            var rows = stuck
                .Select(c => (
                    FullName: string.Join(" ", new[] { c.FirstName, c.MiddleName, c.LastName }
                        .Where(n => !string.IsNullOrWhiteSpace(n))),
                    PassportNumber: (string?)c.PassportNumber,
                    Detail: (string?)BotMessages.DaysInStage(
                        WorkQueueRules.DaysInStage(c.StageEnteredAt, now), c.CurrentStageName, amLang)))
                .ToList();

            var title = amLang ? "የቆዩ" : "Overdue";
            foreach (var page in BotMessages.PeopleList(
                         title, rows, BotMessages.NothingOverdue(amLang), amLang))
                await _telegram.SendMessageAsync(update.ChatId, page, ct);
            return;
        }

        if (parsed.Command == BotCommand.Expiring)
        {
            await using var db = await _tenantFactory.CreateAsync(user.TenantId, user.IsSuperAdmin, ct);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var horizon = WorkQueueRules.ExpiryHorizon(today);

            var expiring = await db.Candidates.AsNoTracking()
                .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active
                            && c.PassportExpiryDate != null && c.PassportExpiryDate <= horizon)
                .OrderBy(c => c.PassportExpiryDate)
                .Select(c => new { c.FirstName, c.MiddleName, c.LastName, c.PassportNumber,
                                   c.PassportExpiryDate })
                .Take(BotCandidateSearch.MaxResults)
                .ToListAsync(ct);

            var rows = expiring
                .Select(c => (
                    FullName: string.Join(" ", new[] { c.FirstName, c.MiddleName, c.LastName }
                        .Where(n => !string.IsNullOrWhiteSpace(n))),
                    PassportNumber: (string?)c.PassportNumber,
                    Detail: (string?)BotMessages.PassportAge(
                        c.PassportExpiryDate!.Value.DayNumber - today.DayNumber, amLang)))
                .ToList();

            var title = amLang ? "ፓስፖርት" : "Passports";
            foreach (var page in BotMessages.PeopleList(
                         title, rows, BotMessages.NothingExpiring(amLang), amLang))
                await _telegram.SendMessageAsync(update.ChatId, page, ct);
            return;
        }

        if (parsed.Command == BotCommand.Me)
        {
            // Overdue and expiring in one message, which is what My work shows on the web.
            await using var db = await _tenantFactory.CreateAsync(user.TenantId, user.IsSuperAdmin, ct);
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var active = db.Candidates.AsNoTracking()
                .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

            var overdueCount = await active.CountAsync(
                c => c.StageEnteredAt != null && c.StageEnteredAt < WorkQueueRules.OverdueCutoff(now), ct);
            var expiringCount = await active.CountAsync(
                c => c.PassportExpiryDate != null
                     && c.PassportExpiryDate <= WorkQueueRules.ExpiryHorizon(today), ct);
            var exceptions = await db.ExceptionCases.AsNoTracking()
                .CountAsync(e => !e.IsDeleted && e.Status == ExceptionStatus.Open, ct);

            await _telegram.SendMessageAsync(update.ChatId,
                BotMessages.MyWork(overdueCount, expiringCount, exceptions, amLang), ct);
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
                        (am ? "ደረጃ አልተገኘም። ያሉት: " : "Stage not found. Available: ")
                            + string.Join(", ", names.Select(BotMessages.Escape)),
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
                    am
                        ? $"📊 <b>{BotMessages.Escape(stage.Name)}</b> — {inStage} እጩ"
                        : $"📊 <b>{BotMessages.Escape(stage.Name)}</b> — {PluralText.Count(inStage, "candidate")}",
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
                        var parts = counts.OrderByDescending(kv => kv.Value)
                            .Select(kv => $"{BotMessages.Escape(kv.Key)} {kv.Value}");
                        lines.Add($"• {BotMessages.Escape(track)}: {string.Join(", ", parts)}");
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
                    : $"📊 {BotStatsRules.PeriodLabel(p, false)}: {PluralText.Count(registered, "new registration")}\nTotal active: {total}";

                var stageLines = stages
                    .Select(s => $"• {BotMessages.Escape(s.Name)}: {byStage.GetValueOrDefault(s.Id, 0)}")
                    .ToList();

                reply = header + "\n\n" + (am ? "በደረጃ:" : "By stage:") + "\n" + string.Join("\n", stageLines)
                    + "\n\n" + BotMessages.StatsFooter(am);
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
    /// Sends a match list, split across messages when it is longer than Telegram allows.
    /// </summary>
    private async Task SendCandidateListAsync(
        string chatId, List<Candidate> matches, string query, bool amharic, CancellationToken ct)
    {
        var rows = matches
            .Select(c => (c.FullName, c.PassportNumber, (string?)c.CurrentStageName))
            .ToList();

        var pages = BotMessages.CandidateList(
            rows, query, truncated: matches.Count >= BotCandidateSearch.MaxResults, amharic);

        foreach (var page in pages)
            await _telegram.SendMessageAsync(chatId, page, ct);
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
    /// <summary>
    /// What a button asked for and is waiting to have answered by the next message.
    ///
    /// A Telegram keyboard button can only send a fixed string, so a command that needs an
    /// argument cannot be one tap. The button asks the question instead and this remembers what
    /// was asked, for five minutes — long enough to type a name, short enough that a message
    /// sent tomorrow is read as a fresh search rather than an answer to a forgotten question.
    /// </summary>
    private static readonly TimeSpan PromptWindow = TimeSpan.FromMinutes(5);

    private static string PromptKey(string chatId) => $"bot:prompt:{chatId}";

    private void AskFor(string chatId, BotCommand command) =>
        _cache.Set(PromptKey(chatId), command, PromptWindow);

    private BotCommand? TakePendingPrompt(string chatId)
    {
        if (!_cache.TryGetValue<BotCommand>(PromptKey(chatId), out var pending)) return null;
        _cache.Remove(PromptKey(chatId));
        return pending;
    }

    private static async Task<List<Candidate>> FindCandidatesAsync(
        ITenantDbContext db, string query, CancellationToken ct)
    {
        var trimmed = query.Trim();

        if (BotCandidateSearch.LooksLikePassport(trimmed))
        {
            var byPassport = await db.Candidates.AsNoTracking()
                // The CV prints every posting, so the history has to come with the candidate.
                .Include(c => c.WorkExperiences)
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
            .Include(c => c.WorkExperiences)
            .Where(c => !c.IsDeleted &&
                (c.FirstName.ToLower().Contains(lead)
                 || c.LastName.ToLower().Contains(lead)
                 || (c.MiddleName != null && c.MiddleName.ToLower().Contains(lead))
                 || (c.LocalFullName != null && c.LocalFullName.ToLower().Contains(lead))))
            .OrderBy(c => c.FirstName).ThenBy(c => c.MiddleName).ThenBy(c => c.LastName)
            .Take(BotCandidateSearch.MaxResults)
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
