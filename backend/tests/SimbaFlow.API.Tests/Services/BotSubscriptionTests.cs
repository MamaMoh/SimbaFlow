using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Who hears about what.
///
/// Everyone linked used to get every move in the agency, so an embassy officer's phone carried
/// thirty messages a day about other people's work — and what people do about that is turn the
/// whole bot off, which is how an agency ends up with nobody reachable.
/// </summary>
public class BotSubscriptionTests
{
    [Fact]
    public void WatchingNothingInParticularMeansEverything()
    {
        BotSubscription.Wants(muted: false, watchStages: null, stageName: "Embassy").Should().BeTrue();
        BotSubscription.Wants(false, "", "Ticket").Should().BeTrue();
    }

    [Fact]
    public void AMutedPersonHearsNothingAtAll()
    {
        BotSubscription.Wants(muted: true, watchStages: null, stageName: "Embassy").Should().BeFalse();
        BotSubscription.Wants(true, "Embassy", "Embassy").Should().BeFalse();
    }

    [Theory]
    [InlineData("Embassy, LMIS", "Embassy", true)]
    [InlineData("Embassy, LMIS", "LMIS", true)]
    [InlineData("Embassy, LMIS", "Ticket", false)]
    [InlineData("embassy", "Embassy", true)]          // typed in lower case on a phone
    [InlineData("Embassy", "Embassy Review", true)]   // the stage was renamed around the word
    public void AStageListLetsThroughOnlyThoseStages(string watch, string stage, bool expected)
    {
        BotSubscription.Wants(false, watch, stage).Should().Be(expected);
    }

    [Fact]
    public void SomethingWithNoStageIsNeverFilteredOutByAStageList()
    {
        // A passport expiring belongs to no board. No stage is not the wrong stage.
        BotSubscription.Wants(false, "Embassy", null).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData("*")]
    public void TheseAllMeanEveryStage(string argument) =>
        BotSubscription.MeansEverything(argument).Should().BeTrue();

    [Fact]
    public void ATypedListIsTidiedBeforeItIsStored()
    {
        BotSubscription.Format(" Embassy , , lmis ,Embassy ".Split(','))
            .Should().Be("Embassy, lmis");
        BotSubscription.Format([" ", ""]).Should().BeNull();
    }
}
