using Carter;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Options;
using SimbaFlow.Infrastructure.Services.Bot;

namespace SimbaFlow.API.Features.Bot;

public class BotModule : ICarterModule
{
    private const string OwnerOnly =
        "Who is linked to the agency's bot is shown to the agency owner and the platform team.";

    /// <summary>
    /// The agency's owner, or the platform team. <c>AgencyOwner</c> is a role that cannot be
    /// granted from the roles screen, which is what makes it mean "answers for this agency"
    /// rather than "was given a tick box by somebody inside it".
    /// </summary>
    private static bool IsAgencyOwner(ICurrentUserService currentUser) =>
        currentUser.IsSuperAdmin
        || AgencyRoles.Owns(currentUser.Roles)
        || currentUser.HasPermission("system.admin");

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bot")
            .WithTags("Bot")
            .RequireAuthorization();

        group.MapGet("/status", async (
            ICurrentUserService currentUser,
            ITelegramPollerState state,
            IOptionsMonitor<TelegramOptions> options) =>
        {
            if (!currentUser.HasPermission("bot.configure") && !currentUser.HasPermission("bot.use"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            return Results.Ok(new
            {
                isSuccess = true,
                data = new
                {
                    configured = state.IsConfigured,
                    enabled = options.CurrentValue.Enabled,
                    pollingEnabled = options.CurrentValue.PollingEnabled,
                    isConnected = state.IsConnected,
                    state.BotUsername,
                    state.LastError,
                    state.LastConnectedAt
                }
            });
        });

        // ──── The agency's own off switch ────
        //
        // Separate from a person unlinking their own chat: this stops the bot answering anyone
        // in the agency and stops every notification, without staff having to unlink one by one.
        group.MapGet("/agency", async (
            ICurrentUserService currentUser,
            IPlatformDbContext platform,
            CancellationToken ct) =>
        {
            if (!currentUser.HasPermission("bot.configure"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);
            if (currentUser.TenantId is not Guid tenantId)
                return Results.Ok(new { isSuccess = true, data = new { botDisabled = false } });

            var settings = await platform.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId && !t.IsDeleted)
                .Select(t => t.Settings)
                .FirstOrDefaultAsync(ct);

            return Results.Ok(new
            {
                isSuccess = true,
                data = new { botDisabled = settings?.BotDisabled == true },
            });
        });

        group.MapPut("/agency", async (
            AgencyBotSettingsBody body,
            ICurrentUserService currentUser,
            IPlatformDbContext platform,
            CancellationToken ct) =>
        {
            if (!currentUser.HasPermission("bot.configure"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);
            if (currentUser.TenantId is not Guid tenantId)
                return Results.Json(
                    new { isSuccess = false, error = "No agency is selected." }, statusCode: 400);

            var tenant = await platform.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Agency not found." }, statusCode: 404);

            // Settings is a JSON column behind a value comparer that snapshots by serialising,
            // so mutating the tracked instance is seen as a change without touching the entry.
            var settings = tenant.Settings ?? new Domain.Entities.Tenancy.TenantSettings();
            settings.BotDisabled = body.BotDisabled;
            tenant.Settings = settings;
            await platform.SaveChangesAsync(ct);

            return Results.Ok(new { isSuccess = true, data = new { botDisabled = settings.BotDisabled } });
        });

        group.MapPost("/test", async (
            ICurrentUserService currentUser,
            ITelegramGateway telegram,
            ITelegramPollerState state) =>
        {
            if (!currentUser.HasPermission("bot.configure"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var me = await telegram.GetMeAsync();
            if (me is null)
                return Results.Json(new { isSuccess = false, error = "Telegram connection failed" }, statusCode: 400);

            state.IsConnected = true;
            state.BotUsername = me.Username;
            state.LastConnectedAt = DateTime.UtcNow;
            state.LastError = null;

            return Results.Ok(new { isSuccess = true, data = me });
        });

        group.MapPost("/link-code", async (
            ICurrentUserService currentUser,
            IBotLinkService linkService) =>
        {
            if (!currentUser.HasPermission("bot.use") || !Guid.TryParse(currentUser.UserId, out var userId))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var result = await linkService.CreateLinkCodeAsync(userId);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapDelete("/link", async (
            ICurrentUserService currentUser,
            IBotLinkService linkService) =>
        {
            if (!currentUser.HasPermission("bot.use") || !Guid.TryParse(currentUser.UserId, out var userId))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var result = await linkService.UnlinkCurrentUserAsync(userId);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // ──── My own linked phones ────
        //
        // An account may have several. The card used to show none of them, so a second phone
        // silently replacing the first was invisible from the web application.
        group.MapGet("/links", async (
            ICurrentUserService currentUser,
            IBotLinkService linkService,
            CancellationToken ct) =>
        {
            if (!currentUser.HasPermission("bot.use") || !Guid.TryParse(currentUser.UserId, out var userId))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var result = await linkService.ListForUserAsync(userId, ct);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapDelete("/links/{id:guid}", async (
            Guid id,
            ICurrentUserService currentUser,
            IBotLinkService linkService,
            CancellationToken ct) =>
        {
            if (!currentUser.HasPermission("bot.use") || !Guid.TryParse(currentUser.UserId, out var userId))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var result = await linkService.RemoveOwnLinkAsync(userId, id, ct);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // ──── My own on/off switch ────
        //
        // /mute and /unmute have always existed in the chat, which is no use to somebody who
        // has not opened Telegram and wants the messages to stop. Same flag, read and written
        // from the settings card.
        group.MapGet("/notifications", async (
            ICurrentUserService currentUser,
            IBotLinkService linkService,
            CancellationToken ct) =>
        {
            if (!currentUser.HasPermission("bot.use") || !Guid.TryParse(currentUser.UserId, out var userId))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var result = await linkService.GetNotificationsOnAsync(userId, ct);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPut("/notifications", async (
            SetBotNotificationsRequest body,
            ICurrentUserService currentUser,
            IBotLinkService linkService,
            CancellationToken ct) =>
        {
            if (!currentUser.HasPermission("bot.use") || !Guid.TryParse(currentUser.UserId, out var userId))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var result = await linkService.SetNotificationsOnAsync(userId, body.On, ct);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // ──── Everyone the agency's bot talks to ────
        //
        // The owner's own bot is answering these people with their agency's data, so the owner is
        // the one who has to be able to see the list and cut a link — somebody who has left, or a
        // phone that was lost. Deliberately not behind a permission: `bot.configure` is grantable
        // from inside the agency, and who is reachable on their own phone is not a roster to hand
        // out with a tick box. The platform team reaches it too, for support.
        group.MapGet("/agency/links", async (
            ICurrentUserService currentUser,
            IBotLinkService linkService,
            CancellationToken ct) =>
        {
            if (!IsAgencyOwner(currentUser))
                return Results.Json(new { isSuccess = false, error = OwnerOnly }, statusCode: 403);
            if (currentUser.TenantId is not Guid tenantId)
                return Results.Json(
                    new { isSuccess = false, error = "No agency is selected." }, statusCode: 400);

            var result = await linkService.ListForAgencyAsync(tenantId, ct);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapDelete("/agency/links/{id:guid}", async (
            Guid id,
            ICurrentUserService currentUser,
            IBotLinkService linkService,
            CancellationToken ct) =>
        {
            if (!IsAgencyOwner(currentUser))
                return Results.Json(new { isSuccess = false, error = OwnerOnly }, statusCode: 403);
            if (currentUser.TenantId is not Guid tenantId)
                return Results.Json(
                    new { isSuccess = false, error = "No agency is selected." }, statusCode: 400);

            var result = await linkService.RemoveAgencyLinkAsync(tenantId, id, ct);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // page/pageSize are optional — the handler already normalizes them, and declaring them as
        // required int made a plain GET /deliveries throw before reaching the handler.
        group.MapGet("/deliveries", async (
            int? page,
            int? pageSize,
            ICurrentUserService currentUser,
            IPlatformDbContext platform,
            UserManager<ApplicationUser> userManager) =>
        {
            if (!currentUser.HasPermission("notification.configure"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var pageNumber = page is null or <= 0 ? 1 : page.Value;
            var size = pageSize is null or <= 0 ? 20 : Math.Min(pageSize.Value, 100);

            var query = platform.NotificationDeliveries.AsNoTracking().OrderByDescending(x => x.CreatedAt);

            if (!currentUser.IsSuperAdmin && currentUser.TenantId.HasValue)
                query = query.Where(x => x.TenantId == currentUser.TenantId.Value).OrderByDescending(x => x.CreatedAt);

            var total = await query.CountAsync();
            var rows = await query.Skip((pageNumber - 1) * size).Take(size)
                .Select(x => new
                {
                    x.Id,
                    x.TenantId,
                    x.UserId,
                    Channel = x.Channel.ToString(),
                    x.EventType,
                    x.PayloadSummary,
                    Status = x.Status.ToString(),
                    x.SentAt,
                    x.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new
            {
                isSuccess = true,
                data = new { totalCount = total, items = rows }
            });
        });
    }
}

/// <summary>Body of the agency bot switch. A class, so a single bool binds by name.</summary>
public sealed class AgencyBotSettingsBody
{
    public bool BotDisabled { get; set; }
}

/// <summary>Body of a person's own notification switch. A class, for the same reason.</summary>
public sealed class SetBotNotificationsRequest
{
    public bool On { get; set; }
}
