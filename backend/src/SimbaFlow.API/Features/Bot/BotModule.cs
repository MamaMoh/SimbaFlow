using Carter;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Infrastructure.Options;
using SimbaFlow.Infrastructure.Services.Bot;

namespace SimbaFlow.API.Features.Bot;

public class BotModule : ICarterModule
{
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
