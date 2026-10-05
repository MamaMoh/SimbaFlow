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
    public void AmbiguousNamesAreListedRatherThanGuessedAt()
    {
        var text = BotMessages.CandidateChoices(
            [("ALMAZ BEKELE", "EP1", "Embassy"), ("ALMAZ TESFAYE", "EP2", "LMIS")], 2, false);

        text.Should().Contain("ALMAZ BEKELE");
        text.Should().Contain("ALMAZ TESFAYE");
        text.Should().Contain("EP1");
        text.Should().Contain("EP2");
    }

    [Fact]
    public void AVeryCommonNameSaysHowManyMoreThereAre()
    {
        var text = BotMessages.CandidateChoices([("ALMAZ BEKELE", "EP1", null)], 12, false);

        text.Should().Contain("12");
        text.Should().Contain("11 more");
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
