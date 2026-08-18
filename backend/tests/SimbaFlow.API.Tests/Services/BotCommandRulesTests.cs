using FluentAssertions;
using SimbaFlow.Domain.Services;
using Xunit;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The bot has to accept three input styles from field staff: keyboard buttons, slash
/// commands, and a bare passport/name with no command at all.
/// </summary>
public class BotCommandRulesTests
{
    [Theory]
    [InlineData("/stats", BotCommand.Stats, "")]
    [InlineData("/stats week", BotCommand.Stats, "week")]
    [InlineData("/STATS Embassy", BotCommand.Stats, "Embassy")]
    [InlineData("/stat month", BotCommand.Stats, "month")]
    [InlineData("/report year", BotCommand.Stats, "year")]
    [InlineData("/cv EP1234567", BotCommand.Cv, "EP1234567")]
    [InlineData("/status EP1234567", BotCommand.Status, "EP1234567")]
    [InlineData("/find Almaz", BotCommand.Status, "Almaz")]
    [InlineData("/search Almaz", BotCommand.Status, "Almaz")]
    [InlineData("/lang am", BotCommand.Language, "am")]
    [InlineData("/language en", BotCommand.Language, "en")]
    [InlineData("/help", BotCommand.Help, "")]
    [InlineData("/menu", BotCommand.Help, "")]
    [InlineData("/start", BotCommand.Start, "")]
    [InlineData("/register", BotCommand.Start, "")]
    [InlineData("/link ABC123", BotCommand.Link, "ABC123")]
    public void ParsesSlashCommandsAndAliases(string input, BotCommand expected, string arg)
    {
        var result = BotCommandRules.Parse(input);
        result.Command.Should().Be(expected);
        result.Argument.Should().Be(arg);
    }

    [Fact]
    public void StripsBotMentionFromCommand()
    {
        // Telegram appends @botname when a command is used in a group chat.
        BotCommandRules.Parse("/stats@simbaflow_bot week")
            .Should().Be(new BotCommandParse(BotCommand.Stats, "week"));
    }

    [Theory]
    [InlineData("📊 Stats", BotCommand.Stats)]
    [InlineData("❓ Help", BotCommand.Help)]
    [InlineData("🌐 Language", BotCommand.Language)]
    public void KeyboardButtonsMapToCommands(string caption, BotCommand expected)
    {
        BotCommandRules.Parse(caption).Command.Should().Be(expected);
    }

    [Theory]
    [InlineData("EP1234567")]
    [InlineData("Almaz Tesfaye")]
    [InlineData("  almaz  ")]
    public void BareTextIsTreatedAsASearch(string input)
    {
        var result = BotCommandRules.Parse(input);
        result.Command.Should().Be(BotCommand.Search);
        result.Argument.Should().Be(input.Trim());
    }

    [Fact]
    public void EmptyInputIsUnknown()
    {
        BotCommandRules.Parse("   ").Command.Should().Be(BotCommand.Unknown);
        BotCommandRules.Parse(null).Command.Should().Be(BotCommand.Unknown);
    }

    [Fact]
    public void UnrecognisedSlashCommandIsUnknownNotASearch()
    {
        // Otherwise a typo like /statuss would be looked up as a candidate name.
        BotCommandRules.Parse("/nonsense arg").Command.Should().Be(BotCommand.Unknown);
    }

    [Fact]
    public void KeyboardJsonIsWellFormedAndPersistent()
    {
        var json = System.Text.Json.JsonDocument.Parse(BotCommandRules.KeyboardJson);
        json.RootElement.GetProperty("resize_keyboard").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("is_persistent").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("keyboard").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void HelpTextIsLocalisedAndMentionsBareSearch()
    {
        BotCommandRules.HelpText(amharic: false).Should().Contain("passport number");
        BotCommandRules.HelpText(amharic: true).Should().Contain("ፓስፖርት");
    }
}
