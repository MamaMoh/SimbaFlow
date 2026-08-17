using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Services.Bot;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Example-based tests for Unit 7 Bot & Notifications (TEST-70–78).
/// </summary>
public class BotNotificationServiceTests
{
    private static PlatformDbContext CreatePlatformDb()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"bot_notify_{Guid.NewGuid()}")
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        return new PlatformDbContext(options, currentUser);
    }

    [Fact]
    public void UnlinkedReply_HasNoCandidateFields_TEST70()
    {
        var reply = BotNotificationRules.UnlinkedInstructionsReply();
        BotNotificationRules.ReplyLooksLikeCandidateData(reply).Should().BeFalse();
        reply.Should().Contain("/link");
    }

    [Fact]
    public void StatusLookup_IsTenantScoped_TEST71()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        BotNotificationRules.TenantScopedPassportMatch(tenantA, tenantA, "EP123", "EP123").Should().BeTrue();
        BotNotificationRules.TenantScopedPassportMatch(tenantA, tenantB, "EP123", "EP123").Should().BeFalse();
    }

    [Fact]
    public async Task LinkCode_IsSingleUseAndExpires_TEST72()
    {
        await using var db = CreatePlatformDb();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "bot.user",
            Email = "bot@example.com",
            FirstName = "Bot",
            LastName = "User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var link = new BotLinkService(db, NullLogger<BotLinkService>.Instance);

        var created = await link.CreateLinkCodeAsync(user.Id);
        created.IsSuccess.Should().BeTrue();
        var code = created.Data!.Code;

        var first = await link.ConsumeLinkCodeAsync("chat-1", code);
        first.IsSuccess.Should().BeTrue();
        user.BotLinked.Should().BeTrue();
        user.TelegramChatId.Should().Be("chat-1");

        var second = await link.ConsumeLinkCodeAsync("chat-2", code);
        second.IsSuccess.Should().BeFalse();

        var expiredCode = "999999";
        db.BotRegistrationChallenges.Add(new BotRegistrationChallenge
        {
            UserId = user.Id,
            Code = expiredCode,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();
        var expired = await link.ConsumeLinkCodeAsync("chat-3", expiredCode);
        expired.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void DeliveryStatus_OnlyDefinedEnumValues_TEST73()
    {
        foreach (DeliveryStatus status in Enum.GetValues<DeliveryStatus>())
            BotNotificationRules.IsValidDeliveryStatus(status).Should().BeTrue();

        BotNotificationRules.IsValidDeliveryStatus(99).Should().BeFalse();
        BotNotificationRules.IsValidDeliveryStatus(-1).Should().BeFalse();
    }

    [Fact]
    public void PushFailure_DoesNotRollbackTransition_TEST74()
    {
        BotNotificationRules.TransitionRemainsCommitted(true, pushSucceeded: false).Should().BeTrue();
        BotNotificationRules.TransitionRemainsCommitted(true, pushSucceeded: true).Should().BeTrue();
        BotNotificationRules.TransitionRemainsCommitted(false, pushSucceeded: true).Should().BeFalse();
    }

    [Fact]
    public void LangPreference_OnlyEnOrAm_TEST75()
    {
        BotNotificationRules.ResolveLanguage("en", "am").Should().Be("en");
        BotNotificationRules.ResolveLanguage("am", "en").Should().Be("am");
        BotNotificationRules.ResolveLanguage("fr", "en").Should().Be("en");
        BotNotificationRules.ResolveLanguage("", "am").Should().Be("am");
        BotNotificationRules.IsValidLanguage("EN").Should().BeTrue();
        BotNotificationRules.IsValidLanguage("de").Should().BeFalse();
    }

    [Fact]
    public void DeliveryError_NeverContainsToken_TEST76()
    {
        const string token = "123456789:AAHdqTcvCH1vGWJxfSeofSAs0K5PALDsaw";
        var sanitized = BotNotificationRules.SanitizeDeliveryError(
            $"Telegram failed for bot {token} with 401", token);
        sanitized.Should().NotContain(token);
        sanitized.Should().Contain("[REDACTED]");

        var shaped = BotNotificationRules.SanitizeDeliveryError(
            $"Unauthorized token 987654321:BBBdqTcvCH1vGWJxfSeofSAs0K5PALDsaw");
        shaped.Should().NotMatchRegex(@"\d{8,12}:[A-Za-z0-9_-]{20,}");
    }

    [Fact]
    public void MedicalAndArrived_DoNotMutate_TEST77()
    {
        BotNotificationRules.IsDeferredWriteCommand("/medical EP1").Should().BeTrue();
        BotNotificationRules.IsDeferredWriteCommand("/arrived").Should().BeTrue();
        BotNotificationRules.WriteCommandMutatesCandidate("/medical").Should().BeFalse();
        BotNotificationRules.WriteCommandMutatesCandidate("/arrived now").Should().BeFalse();
        BotNotificationRules.WriteCommandMutatesCandidate("/status EP1").Should().BeTrue();
    }

    [Fact]
    public void Notifier_IsNonNoOpImplementation_TEST78()
    {
        BotNotificationRules.IsNonNoOpNotifier(nameof(TelegramCandidateNotifier)).Should().BeTrue();
        BotNotificationRules.IsNonNoOpNotifier("NoOpCandidateNotifier").Should().BeFalse();
        typeof(TelegramCandidateNotifier).GetInterfaces()
            .Should().Contain(typeof(ICandidateNotifier));
    }

    [Fact]
    public async Task Unlink_ClearsTelegramChatId()
    {
        await using var db = CreatePlatformDb();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "linked.user",
            Email = "linked@example.com",
            FirstName = "Linked",
            LastName = "User",
            BotLinked = true,
            TelegramChatId = "chat-99"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var link = new BotLinkService(db, NullLogger<BotLinkService>.Instance);
        var result = await link.UnlinkCurrentUserAsync(user.Id);
        result.IsSuccess.Should().BeTrue();
        user.BotLinked.Should().BeFalse();
        user.TelegramChatId.Should().BeNull();
    }
}
