using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// How the bot reads to the person holding the phone.
///
/// The chat was showing internal event keys ("FOZIYA SEID YIMER - departure.notified") and one
/// message per stage hop, eight in seven minutes for a single candidate. These pin the wording so
/// neither comes back.
/// </summary>
public class BotMessagesTests
{
    [Fact]
    public void AnInternalEventKeyNeverReachesTheUser()
    {
        var text = BotMessages.EventHeadline("FOZIYA SEID YIMER", "EQ1030621", "departure.notified", false);

        text.Should().NotContain("departure.notified");
        text.Should().Contain("FOZIYA SEID YIMER");
        text.Should().Contain("flight");
    }

    [Fact]
    public void AnUnmappedKeyFallsBackToTheNameRatherThanPrintingTheKey()
    {
        // A key the mapping has not caught up with tells the reader nothing, so say nothing.
        var text = BotMessages.EventHeadline("FOZIYA SEID YIMER", "EQ1030621", "some.new.event", false);

        text.Should().NotContain("some.new.event");
        text.Should().Contain("FOZIYA SEID YIMER");
    }

    [Fact]
    public void AStageMoveReadsAsAJourneyNotAnEvent()
    {
        // Written as a position so the same message can be rewritten in place on the next hop.
        var text = BotMessages.StageMoved(
            "FOZIYA SEID YIMER", "EQ1030621", "New Contracts", "Commission", "mohammed", false);

        text.Should().Contain("New Contracts");
        text.Should().Contain("Commission");
        text.Should().Contain("→");
        text.Should().Contain("mohammed");
    }

    [Fact]
    public void AFirstSightingHasNoArrowToPointBackAt()
    {
        var text = BotMessages.StageMoved("FOZIYA SEID YIMER", "EQ1030621", null, "Embassy", null, false);

        text.Should().NotContain("→");
        text.Should().Contain("Embassy");
    }

    [Theory]
    [InlineData("Tsehay & Co")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("a > b < c")]
    public void DatabaseValuesCannotBreakOutOfTheMarkup(string name)
    {
        // Messages go out as Telegram HTML; an unescaped name would break the message, and an
        // unescaped tag would be worse.
        var card = BotMessages.CandidateCard(name, "EQ1030621", "Embassy", "Active", null, false);

        card.Should().NotContain("<script>");
        card.Should().NotContain(" & ");
        BotMessages.Escape(name).Should().NotContain("<script>");
    }

    [Fact]
    public void ACandidateCardCarriesThePassportBecauseThatIsWhatStaffQuote()
    {
        var card = BotMessages.CandidateCard(
            "SEADA TESFAYE MEKONNEN", "EQ1030621", "LMIS", "Active", "Saudi Arabia", false);

        card.Should().Contain("SEADA TESFAYE MEKONNEN");
        card.Should().Contain("EQ1030621");
        card.Should().Contain("LMIS");
        card.Should().Contain("Saudi Arabia");
        card.Should().Contain("/cv EQ1030621", "the next thing anyone wants is the CV");
    }

    [Fact]
    public void EveryMatchIsListed_NotASample()
    {
        // Typing a first name is asking "who do we have called this?" — the answer is all of them.
        var many = Enumerable.Range(1, 30)
            .Select(i => ($"ALMAZ CANDIDATE {i}", $"EP{i:0000000}", (string?)"Embassy"))
            .ToList();

        var pages = BotMessages.CandidateList(many, "almaz", truncated: false, amharic: false);
        var all = string.Join("\n", pages);

        all.Should().Contain("ALMAZ CANDIDATE 1");
        all.Should().Contain("ALMAZ CANDIDATE 30");
        all.Should().Contain("<b>30</b> candidates");
        foreach (var (_, passport, _) in many)
            all.Should().Contain(passport);
    }

    [Fact]
    public void ALongListIsSplitSoTelegramAcceptsIt()
    {
        // Telegram rejects anything over 4096 characters outright — one oversized message is a
        // search that silently returns nothing.
        var many = Enumerable.Range(1, 200)
            .Select(i => ($"CANDIDATE WITH A FAIRLY LONG NAME {i}", $"EP{i:0000000}", (string?)"New Contracts"))
            .ToList();

        var pages = BotMessages.CandidateList(many, "candidate", truncated: true, amharic: false);

        pages.Count.Should().BeGreaterThan(1);
        pages.Should().OnlyContain(p => p.Length <= 4096);
        string.Join("\n", pages).Should().Contain("CANDIDATE WITH A FAIRLY LONG NAME 200");
    }

    [Fact]
    public void AShortListFitsInOneMessage()
    {
        var pages = BotMessages.CandidateList(
            [("ALMAZ BEKELE", "EP1", "Embassy"), ("ALMAZ TESFAYE", "EP2", "LMIS")],
            "almaz", truncated: false, amharic: false);

        pages.Should().ContainSingle();
        pages[0].Should().Contain("ALMAZ BEKELE").And.Contain("ALMAZ TESFAYE");
    }

    [Fact]
    public void HittingTheCapSaysSoRatherThanPretendingThatIsEverybody()
    {
        var pages = BotMessages.CandidateList(
            [("A B", "EP1", null)], "a", truncated: true, amharic: false);

        string.Join("\n", pages).Should().Contain("narrow");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothLanguagesAnswerEveryPrompt(bool amharic)
    {
        // An empty string here is a user staring at a blank reply.
        BotMessages.Welcome(amharic).Should().NotBeNullOrWhiteSpace();
        BotMessages.Help(amharic).Should().NotBeNullOrWhiteSpace();
        BotMessages.NotUnderstood(amharic).Should().NotBeNullOrWhiteSpace();
        BotMessages.AskForCandidate(amharic).Should().NotBeNullOrWhiteSpace();
        BotMessages.CandidateNotFound("x", amharic).Should().NotBeNullOrWhiteSpace();
        BotMessages.NoAgency(amharic).Should().NotBeNullOrWhiteSpace();
        BotMessages.NoStatsPermission(amharic).Should().NotBeNullOrWhiteSpace();
        BotMessages.WebAppOnly(amharic).Should().NotBeNullOrWhiteSpace();
        BotMessages.LanguageChoices(amharic).Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("mohammed", "mohammed", false)]
    [InlineData("mohammed", "MOHAMMED", false)]
    [InlineData("hana", "mohammed", true)]
    [InlineData("hana", null, true)]
    public void NobodyIsToldAboutTheirOwnClick(string recipient, string? actor, bool expected)
    {
        BotNotificationRules.ShouldNotify(recipient, actor).Should().Be(expected);
    }
}
