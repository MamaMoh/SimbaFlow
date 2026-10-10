using System.Text.Json;
using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Everything a thumb can press has to reach a command, and everything in the "/" menu has to
/// be a command the dispatcher knows.
///
/// These drift apart silently: a button caption is a string compared character by character, so
/// changing an emoji in one place and not the other produces a button that falls through to
/// candidate search and answers "no candidate called 📋 Stage list".
/// </summary>
public class BotCommandSurfaceTests
{
    private static IReadOnlyList<string> KeyboardCaptions()
    {
        using var doc = JsonDocument.Parse(BotCommandRules.KeyboardJson);
        return doc.RootElement.GetProperty("keyboard")
            .EnumerateArray()
            .SelectMany(row => row.EnumerateArray())
            .Select(b => b.GetProperty("text").GetString()!)
            .ToList();
    }

    [Fact]
    public void EveryKeyboardButtonParsesToSomethingOtherThanASearch()
    {
        foreach (var caption in KeyboardCaptions())
        {
            var parsed = BotCommandRules.Parse(caption);
            parsed.Command.Should().NotBe(BotCommand.Search, $"'{caption}' is a button, not a name");
            parsed.Command.Should().NotBe(BotCommand.Unknown, $"'{caption}' is a button nobody handles");
        }
    }

    [Fact]
    public void TheDestructiveCommandIsNotAButton()
    {
        // /unlink costs you every notification until somebody fetches a new code from the web
        // application. It is typed, deliberately.
        KeyboardCaptions().Should().NotContain(c =>
            BotCommandRules.Parse(c).Command == BotCommand.Unlink);
    }

    [Fact]
    public void EveryCommandInTheSlashMenuIsRecognised()
    {
        using var doc = JsonDocument.Parse(BotCommandRules.MyCommandsJson);
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var verb = entry.GetProperty("command").GetString()!;
            BotCommandRules.Parse($"/{verb}").Command
                .Should().NotBe(BotCommand.Unknown, $"/{verb} is offered in the menu");
        }
    }

    [Theory]
    [InlineData("/stage embassy", BotCommand.Stage, "embassy")]
    [InlineData("/overdue", BotCommand.Overdue, "")]
    [InlineData("/expiring", BotCommand.Expiring, "")]
    [InlineData("/me", BotCommand.Me, "")]
    [InlineData("/unlink yes", BotCommand.Unlink, "yes")]
    [InlineData("/stuck", BotCommand.Overdue, "")]
    [InlineData("/today", BotCommand.Me, "")]
    public void TheNewCommandsParse(string typed, BotCommand command, string argument)
    {
        var parsed = BotCommandRules.Parse(typed);
        parsed.Command.Should().Be(command);
        parsed.Argument.Should().Be(argument);
    }

    [Fact]
    public void ANameIsStillJustANameAfterAllOfThat()
    {
        BotCommandRules.Parse("Abeba Tesfaye").Command.Should().Be(BotCommand.Search);
        BotCommandRules.Parse("EP1234567").Command.Should().Be(BotCommand.Search);
    }
}
