using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Tenancy;

namespace SimbaFlow.Infrastructure.Services.Bot;

public interface IBotLinkService
{
    Task<Result<LinkCodeDto>> CreateLinkCodeAsync(Guid userId, CancellationToken ct = default);
    Task<Result> ConsumeLinkCodeAsync(string chatId, string code, CancellationToken ct = default);
    Task<Result> UnlinkCurrentUserAsync(Guid userId, CancellationToken ct = default);
}

public sealed class BotLinkService : IBotLinkService
{
    private readonly IPlatformDbContext _platform;
    private readonly ILogger<BotLinkService> _logger;

    public BotLinkService(
        IPlatformDbContext platform,
        ILogger<BotLinkService> logger)
    {
        _platform = platform;
        _logger = logger;
    }

    public async Task<Result<LinkCodeDto>> CreateLinkCodeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _platform.ApplicationUsers
            .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, ct);
        if (user is null)
            return Result<LinkCodeDto>.Failure("User not found", 404);

        var code = Random.Shared.Next(100000, 999999).ToString();
        var challenge = new BotRegistrationChallenge
        {
            UserId = userId,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _platform.BotRegistrationChallenges.Add(challenge);
        await _platform.SaveChangesAsync(ct);

        return Result<LinkCodeDto>.Success(new LinkCodeDto(code, challenge.ExpiresAt));
    }

    public async Task<Result> ConsumeLinkCodeAsync(string chatId, string code, CancellationToken ct = default)
    {
        var normalized = code.Trim();
        var challenge = await _platform.BotRegistrationChallenges
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x =>
                x.Code == normalized &&
                x.ConsumedAt == null &&
                x.ExpiresAt > DateTime.UtcNow &&
                !x.IsDeleted, ct);

        if (challenge is null)
            return Result.Failure("Link code is invalid or expired", 400);

        var user = await _platform.ApplicationUsers
            .FirstOrDefaultAsync(x => x.Id == challenge.UserId && !x.IsDeleted, ct);
        if (user is null)
            return Result.Failure("User not found", 404);

        user.TelegramChatId = chatId;
        user.BotLinked = true;
        challenge.ConsumedAt = DateTime.UtcNow;
        await _platform.SaveChangesAsync(ct);

        _logger.LogInformation("Linked Telegram chat for user {UserId}", user.Id);
        return Result.Success();
    }

    public async Task<Result> UnlinkCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _platform.ApplicationUsers
            .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, ct);
        if (user is null)
            return Result.Failure("User not found", 404);

        user.TelegramChatId = null;
        user.BotLinked = false;
        await _platform.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record LinkCodeDto(string Code, DateTime ExpiresAt);
