using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Services.Bot;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Several Telegram accounts on one SimbaFlow account, and the owner's view of them.
///
/// Linking a second phone used to overwrite the first: one column held one chat id, so the
/// office handset went quiet the moment the owner linked their own, with nothing anywhere to say
/// it had happened. And nobody could see who was linked at all — not the person themselves, and
/// not the owner whose agency's data the bot was answering with.
/// </summary>
public class BotMultipleChatLinksTests : IDisposable
{
    private readonly PlatformDbContext _db;
    private readonly BotLinkService _links;
    private readonly Guid _agency = Guid.NewGuid();
    private readonly Guid _otherAgency = Guid.NewGuid();

    public BotMultipleChatLinksTests()
    {
        _db = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase($"bot_links_{Guid.NewGuid()}").Options,
            Substitute.For<ICurrentUserService>());

        _links = new BotLinkService(
            _db, new MemoryCache(new MemoryCacheOptions()), NullLogger<BotLinkService>.Instance);
    }

    private ApplicationUser Staff(string userName, Guid? tenantId = null)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = $"{userName}@example.com",
            FirstName = userName,
            LastName = "Sample",
            TenantId = tenantId ?? _agency,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private async Task LinkAsync(ApplicationUser user, string chatId, string? handle = null)
    {
        var code = await _links.CreateLinkCodeAsync(user.Id);
        var result = await _links.ConsumeLinkCodeAsync(chatId, code.Data!.Code, handle, handle);
        result.IsSuccess.Should().BeTrue(result.Error);
    }

    // ── Several phones on one account ────────────────────────────────────────

    [Fact]
    public async Task ASecondPhoneJoinsTheFirstRatherThanReplacingIt()
    {
        var owner = Staff("owner.sample");

        await LinkAsync(owner, "chat-office");
        await LinkAsync(owner, "chat-personal");

        var mine = await _links.ListForUserAsync(owner.Id);
        mine.Data!.Should().HaveCount(2);
        _db.BotChatLinks.Where(l => l.UserId == owner.Id && !l.IsDeleted)
            .Select(l => l.ChatId)
            .Should().BeEquivalentTo(["chat-office", "chat-personal"]);
    }

    [Fact]
    public async Task BothPhonesAreWrittenTo()
    {
        var owner = Staff("owner.sample");
        await LinkAsync(owner, "chat-office");
        await LinkAsync(owner, "chat-personal");

        var recipients = await BotRecipients.ForTenantAsync(_db, _agency, default);

        recipients.Should().ContainSingle();
        recipients[0].ChatIds.Should().BeEquivalentTo(["chat-office", "chat-personal"]);
    }

    [Fact]
    public async Task TheCapIsEnforced()
    {
        var owner = Staff("owner.sample");
        for (var i = 0; i < BotLinkCodeRules.MaxChatsPerUser; i++)
            await LinkAsync(owner, $"chat-{i}");

        var code = await _links.CreateLinkCodeAsync(owner.Id);
        var oneTooMany = await _links.ConsumeLinkCodeAsync("chat-overflow", code.Data!.Code);

        oneTooMany.IsSuccess.Should().BeFalse();
        oneTooMany.StatusCode.Should().Be(409);
        // The refusal has to say what to do about it, not just that it was refused.
        oneTooMany.Error.Should().Contain("Disconnect one");
    }

    [Fact]
    public async Task ReLinkingTheSamePhoneIsNotASecondLink()
    {
        var owner = Staff("owner.sample");
        await LinkAsync(owner, "chat-office");
        await LinkAsync(owner, "chat-office", "office_phone");

        _db.BotChatLinks.Count(l => l.UserId == owner.Id && !l.IsDeleted).Should().Be(1);
        _db.BotChatLinks.Single(l => !l.IsDeleted).TelegramUsername.Should().Be("office_phone");
    }

    [Fact]
    public async Task OneChatCannotAnswerForTwoAccounts()
    {
        // The bot replies with somebody's agency, role and permissions. A chat resolving to two
        // people would be an access-control question with no answer, so the second is refused
        // rather than quietly redirecting the first person's notifications.
        var first = Staff("first.sample");
        var second = Staff("second.sample");

        await LinkAsync(first, "shared-chat");

        var code = await _links.CreateLinkCodeAsync(second.Id);
        var stolen = await _links.ConsumeLinkCodeAsync("shared-chat", code.Data!.Code);

        stolen.IsSuccess.Should().BeFalse();
        stolen.StatusCode.Should().Be(409);
        stolen.Error.Should().Contain("first.sample");
        (await _links.ResolveUserIdAsync("shared-chat")).Should().Be(first.Id);
    }

    [Fact]
    public async Task UnlinkingOnePhoneLeavesTheOther()
    {
        var owner = Staff("owner.sample");
        await LinkAsync(owner, "chat-office");
        await LinkAsync(owner, "chat-personal");

        var result = await _links.UnlinkChatAsync("chat-office");

        result.IsSuccess.Should().BeTrue();
        (await _links.ResolveUserIdAsync("chat-office")).Should().BeNull();
        (await _links.ResolveUserIdAsync("chat-personal")).Should().Be(owner.Id);
    }

    [Fact]
    public async Task APhoneCanBeLinkedAgainAfterBeingDropped()
    {
        // The rows are soft-deleted and the unique index is filtered to live ones, so a handset
        // that was disconnected is not permanently barred.
        var owner = Staff("owner.sample");
        await LinkAsync(owner, "chat-office");
        await _links.UnlinkChatAsync("chat-office");

        await LinkAsync(owner, "chat-office");

        (await _links.ResolveUserIdAsync("chat-office")).Should().Be(owner.Id);
    }

    [Fact]
    public async Task IdAtTheDoorIsNotAKeyToSomebodyElsesLink()
    {
        var mine = Staff("mine.sample");
        var theirs = Staff("theirs.sample");
        await LinkAsync(theirs, "chat-theirs");

        var link = _db.BotChatLinks.Single(l => l.ChatId == "chat-theirs");
        var result = await _links.RemoveOwnLinkAsync(mine.Id, link.Id);

        result.IsSuccess.Should().BeFalse();
        // Not found and not yours read alike, so one account cannot probe for another's ids.
        result.StatusCode.Should().Be(404);
        (await _links.ResolveUserIdAsync("chat-theirs")).Should().Be(theirs.Id);
    }

    // ── What the owner sees, and can cut ─────────────────────────────────────

    [Fact]
    public async Task TheOwnerSeesEveryLinkInTheAgency()
    {
        var owner = Staff("owner.sample");
        var clerk = Staff("clerk.sample");
        await LinkAsync(owner, "chat-office", "office_phone");
        await LinkAsync(owner, "chat-personal");
        await LinkAsync(clerk, "chat-clerk");

        var roster = await _links.ListForAgencyAsync(_agency);

        roster.Data!.Should().HaveCount(3);
        roster.Data!.Select(r => r.UserName).Should().Contain(["owner.sample", "clerk.sample"]);
        roster.Data!.Should().Contain(r => r.TelegramUsername == "office_phone");
    }

    [Fact]
    public async Task TheRosterStopsAtTheAgencysEdge()
    {
        var mine = Staff("mine.sample");
        var outsider = Staff("outsider.sample", _otherAgency);
        await LinkAsync(mine, "chat-mine");
        await LinkAsync(outsider, "chat-outsider");

        var roster = await _links.ListForAgencyAsync(_agency);

        roster.Data!.Should().ContainSingle();
        roster.Data![0].UserName.Should().Be("mine.sample");
    }

    [Fact]
    public async Task TheRosterDoesNotHandOutChatIds()
    {
        // Nothing on the screen needs Telegram's own id, and it is the one field that would let
        // the link be addressed from outside the application.
        var staff = Staff("staff.sample");
        await LinkAsync(staff, "chat-staff");

        var roster = await _links.ListForAgencyAsync(_agency);

        typeof(AgencyBotChatLinkDto).GetProperties()
            .Select(p => p.Name)
            .Should().NotContain("ChatId");
        roster.Data!.Should().ContainSingle();
    }

    [Fact]
    public async Task TheOwnerCanCutSomebodyElsesLink()
    {
        var clerk = Staff("clerk.sample");
        await LinkAsync(clerk, "chat-clerk");
        var link = _db.BotChatLinks.Single(l => l.ChatId == "chat-clerk");

        var result = await _links.RemoveAgencyLinkAsync(_agency, link.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.UserName.Should().Be("clerk.sample");
        (await _links.ResolveUserIdAsync("chat-clerk")).Should().BeNull();
    }

    [Fact]
    public async Task AnOwnerCannotReachIntoAnotherAgency()
    {
        var outsider = Staff("outsider.sample", _otherAgency);
        await LinkAsync(outsider, "chat-outsider");
        var link = _db.BotChatLinks.Single(l => l.ChatId == "chat-outsider");

        var result = await _links.RemoveAgencyLinkAsync(_agency, link.Id);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        (await _links.ResolveUserIdAsync("chat-outsider")).Should().Be(outsider.Id);
    }

    // ── Keeping track of what is still in use ────────────────────────────────

    [Fact]
    public async Task UsingTheBotKeepsTheLinkLookingAlive()
    {
        var staff = Staff("staff.sample");
        await LinkAsync(staff, "chat-staff");

        var link = await _db.BotChatLinks.FirstAsync(l => l.ChatId == "chat-staff");
        link.LastSeenAt = DateTime.UtcNow.AddDays(-3);
        link.TelegramUsername = null;
        await _db.SaveChangesAsync();

        await _links.TouchAsync("chat-staff", "staff_phone", "Staff Sample");

        var after = await _db.BotChatLinks.FirstAsync(l => l.ChatId == "chat-staff");
        after.LastSeenAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
        after.TelegramUsername.Should().Be("staff_phone");
        after.DisplayName.Should().Be("Staff Sample");
    }

    [Fact]
    public async Task EveryMessageIsNotAWrite()
    {
        // The dispatcher calls this on every single incoming message. An hour's resolution is
        // plenty for a column whose only reader is an owner scanning for links nobody uses.
        var staff = Staff("staff.sample");
        await LinkAsync(staff, "chat-staff", "staff_phone");

        var before = await _db.BotChatLinks
            .Where(l => l.ChatId == "chat-staff").Select(l => l.LastSeenAt).FirstAsync();

        await _links.TouchAsync("chat-staff", "staff_phone", "staff_phone");

        var after = await _db.BotChatLinks
            .Where(l => l.ChatId == "chat-staff").Select(l => l.LastSeenAt).FirstAsync();
        after.Should().Be(before);
    }

    [Fact]
    public async Task TouchingAChatThatIsNotLinkedDoesNothing()
    {
        await _links.TouchAsync("chat-nobody", "someone", "Some One");
        _db.BotChatLinks.Should().BeEmpty();
    }

    public void Dispose() => _db.Dispose();
}
