using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Every event the application raises has words to say about it.
///
/// The messages for medicals, visas, tickets, departures and arrivals were written and
/// translated months before anything called them, and an unmapped key falls back to the
/// candidate's name alone — a notification that says who but not what. So the keys the handlers
/// pass are pinned here against the words they produce.
/// </summary>
public class BotEventMessageTests
{
    public static TheoryData<string> WiredKeys() =>
    [
        "candidate.registered",
        "medical.fit",
        "medical.unfit",
        "visa.issued",
        "visa.rejected",
        "ticket.booked",
        "departure.notified",
        "departure.confirmed",
        "arrival.confirmed",
    ];

    [Theory]
    [MemberData(nameof(WiredKeys))]
    public void TheKeySaysSomethingInBothLanguages(string key)
    {
        foreach (var amharic in new[] { false, true })
        {
            var text = BotMessages.EventHeadline("SAMPLE CANDIDATE", "E00000001", key, amharic);

            // More than the name and passport means the key was recognised.
            text.Should().Contain("SAMPLE CANDIDATE");
            text.Split('\n').Should().HaveCountGreaterThan(1, $"'{key}' should say what happened");
        }
    }

    [Fact]
    public void WhoDidItIsNamedWhenTheCallerKnows()
    {
        BotMessages.EventHeadline("SAMPLE CANDIDATE", "E1", "candidate.registered", false, "sara")
            .Should().Contain("by sara");
    }

    [Fact]
    public void AnUnknownKeyFallsBackToTheNameRatherThanPrintingTheKey()
    {
        var text = BotMessages.EventHeadline("SAMPLE CANDIDATE", "E1", "something.new", false);
        text.Should().Contain("SAMPLE CANDIDATE").And.NotContain("something.new");
    }

    [Fact]
    public void TheOverdueAndExpiryWordsUseTheSameThresholdsAsTheWebApplication()
    {
        BotMessages.NothingOverdue(false).Should().Contain(WorkQueueRules.OverdueThresholdDays.ToString());
        BotMessages.NothingExpiring(false).Should().Contain(WorkQueueRules.ExpirySoonDays.ToString());
    }

    [Fact]
    public void TheCardDropsTheAskForTheCvLineWhenTheCvIsAttachedToIt()
    {
        var lookup = BotMessages.CandidateCard(
            "SAMPLE CANDIDATE", "E00000001", "Embassy", "Active", "Saudi Arabia", amharic: false);
        var withCv = BotMessages.CandidateCard(
            "SAMPLE CANDIDATE", "E00000001", "Embassy", "Active", "Saudi Arabia", amharic: false,
            withCvHint: false);

        // On a lookup the card is the only place that says how to ask for the CV.
        lookup.Should().Contain("/cv E00000001");
        // On the answer to /cv the document is right there, so telling the reader how to ask
        // for it is telling somebody holding a thing how to request that thing.
        withCv.Should().NotContain("/cv");
        withCv.Should().Contain("Embassy");
    }

    [Fact]
    public void TheCardLinksToTheCandidatesPage()
    {
        var id = Guid.NewGuid();
        var card = BotMessages.CandidateCard(
            "SAMPLE CANDIDATE", "E00000001", "Embassy", "Active", "Saudi Arabia", amharic: false,
            profileUrl: BotMessages.ProfileUrl("https://app.example.com/", id));

        card.Should().Contain($"https://app.example.com/candidates/{id}");
        card.Should().Contain("Open the full record");
    }

    [Fact]
    public void NoAddressConfiguredMeansNoLinkRatherThanALinkToNowhere()
    {
        BotMessages.ProfileUrl(null, Guid.NewGuid()).Should().BeNull();
        BotMessages.ProfileUrl("  ", Guid.NewGuid()).Should().BeNull();

        BotMessages.CandidateCard(
                "SAMPLE CANDIDATE", "E1", "Embassy", "Active", "Saudi Arabia", amharic: false,
                profileUrl: null)
            .Should().NotContain("<a href");
    }

    [Fact]
    public void AnEmptyListSaysSoRatherThanPrintingAHeadingWithNothingUnderIt()
    {
        var pages = BotMessages.PeopleList("Embassy", [], "Nobody here.", false);
        pages.Should().ContainSingle().Which.Should().Be("Nobody here.");
    }

    [Fact]
    public void ALongListIsSplitBecauseTelegramRefusesAnythingOver4096()
    {
        var rows = Enumerable.Range(1, 400)
            .Select(i => ($"CANDIDATE NUMBER {i}", (string?)$"E{i:0000000}", (string?)"14 days"))
            .ToList();

        var pages = BotMessages.PeopleList("Embassy", rows, null, false);

        pages.Count.Should().BeGreaterThan(1);
        pages.Should().OnlyContain(p => p.Length <= 4096);
    }
}
