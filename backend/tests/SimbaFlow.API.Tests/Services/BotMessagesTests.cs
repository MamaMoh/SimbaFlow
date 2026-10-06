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

    /// <summary>
    /// Telegram's HTML parse mode supports a short list of tags and rejects the whole message for
    /// anything else — not the offending characters, the message. A reply ending in a literal
    /// "&lt;stage&gt;" made /stats answer with silence for days, because the refusal was a 200
    /// with ok:false that nothing logged.
    /// </summary>
    private static readonly string[] AllowedTags =
        ["b", "/b", "i", "/i", "u", "/u", "s", "/s", "code", "/code", "pre", "/pre",
         "tg-spoiler", "/tg-spoiler", "blockquote", "/blockquote"];

    private static void AssertParsesAsTelegramHtml(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '<') continue;
            var close = text.IndexOf('>', i);
            close.Should().BeGreaterThan(i, $"an unclosed '<' in: {text}");

            var tag = text[(i + 1)..close];
            var ok = AllowedTags.Contains(tag) || tag.StartsWith("a href=") || tag == "/a";
            ok.Should().BeTrue($"\"<{tag}>\" is not a tag Telegram accepts, in: {text}");
            i = close;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryMessageSurvivesTelegramsHtmlParser(bool amharic)
    {
        AssertParsesAsTelegramHtml(BotMessages.StatsFooter(amharic));
        AssertParsesAsTelegramHtml(BotMessages.Welcome(amharic));
        AssertParsesAsTelegramHtml(BotMessages.Help(amharic));
        AssertParsesAsTelegramHtml(BotMessages.AlreadyLinked("Mohammed Anwar", amharic));
        AssertParsesAsTelegramHtml(BotMessages.LinkSucceeded("Mohammed Anwar", "Tango Foreign Employment Agent", amharic));
        AssertParsesAsTelegramHtml(BotMessages.AskForLinkCode(amharic));
        AssertParsesAsTelegramHtml(BotMessages.AskForCandidate(amharic));
        AssertParsesAsTelegramHtml(BotMessages.CandidateNotFound("almaz", amharic));
        AssertParsesAsTelegramHtml(BotMessages.NotUnderstood(amharic));
        AssertParsesAsTelegramHtml(BotMessages.NoAgency(amharic));
        AssertParsesAsTelegramHtml(BotMessages.NoStatsPermission(amharic));
        AssertParsesAsTelegramHtml(BotMessages.WebAppOnly(amharic));
        AssertParsesAsTelegramHtml(BotMessages.LanguageChoices(amharic));
        AssertParsesAsTelegramHtml(BotMessages.LanguageUpdated(amharic));
        AssertParsesAsTelegramHtml(BotMessages.CvBeingPrepared(amharic));
        AssertParsesAsTelegramHtml(BotMessages.SomethingWentWrong(amharic));
        AssertParsesAsTelegramHtml(
            BotMessages.CandidateCard("SEADA MEKONNEN", "EQ1030621", "LMIS", "Active", "Saudi Arabia", amharic));
        AssertParsesAsTelegramHtml(
            BotMessages.StageMoved("FOZIYA YIMER", "EQ1", "New Contracts", "Commission", "mohammed", amharic));
        AssertParsesAsTelegramHtml(
            BotMessages.EventHeadline("FOZIYA YIMER", "EQ1", "departure.notified", amharic));
        foreach (var page in BotMessages.CandidateList(
                     [("ALMAZ BEKELE", "EP1", "Embassy")], "almaz", truncated: true, amharic: amharic))
            AssertParsesAsTelegramHtml(page);
    }

    [Fact]
    public void AnAgencysOwnNamesCannotBreakAMessage()
    {
        // A stage called "Medical & Tasheer" or a candidate called "Tsehay <sic>" goes out as HTML
        // like everything else.
        AssertParsesAsTelegramHtml(
            BotMessages.CandidateCard("Tsehay <sic> & Co", "EQ1", "Medical & Tasheer", "Active", "UAE", false));
        AssertParsesAsTelegramHtml(
            BotMessages.StageMoved("A & B", "EQ1", "X <1>", "Y & Z", "user<1>", false));
    }
}
