using FluentAssertions;
using SimbaFlow.Domain.Services;
using Xunit;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// These codes are the only thing standing between a stranger's Telegram and a real account, so
/// the properties that matter are the security ones, not the formatting.
/// </summary>
public class BotLinkCodeRulesTests
{
    [Fact]
    public void GeneratedCodesAreLongEnoughToNotBeGuessable()
    {
        // Six digits was one million options with no attempt limit. Eight characters over a
        // 30-symbol alphabet is ~6.5e11 — unreachable inside a ten-minute window.
        var code = BotLinkCodeRules.Generate();
        code.Should().HaveLength(BotLinkCodeRules.CodeLength);
        BotLinkCodeRules.CodeLength.Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public void GeneratedCodesAvoidCharactersPeopleConfuse()
    {
        // Codes are read off a screen and typed on a phone; 0/O and 1/I/L cause failed links that
        // look like bugs.
        var sample = string.Concat(Enumerable.Range(0, 400).Select(_ => BotLinkCodeRules.Generate()));
        sample.Should().NotContain("0").And.NotContain("O");
        sample.Should().NotContain("1").And.NotContain("I").And.NotContain("L");
    }

    [Fact]
    public void GeneratedCodesDoNotRepeat()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => BotLinkCodeRules.Generate()).ToList();
        codes.Distinct().Should().HaveCount(codes.Count);
    }

    [Theory]
    [InlineData("abcd2345", "ABCD2345")]
    [InlineData("  ABCD2345  ", "ABCD2345")]
    [InlineData("ABCD-2345", "ABCD2345")]
    [InlineData("ABCD 2345", "ABCD2345")]
    public void NormalizeAcceptsHowPeopleActuallyType(string input, string expected)
    {
        BotLinkCodeRules.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void ThrottleAllowsMistypesButNotProbing()
    {
        BotLinkCodeRules.MaxAttemptsPerChat.Should().BeInRange(3, 10);
        BotLinkCodeRules.AttemptWindow.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMinutes(5));
    }
}
