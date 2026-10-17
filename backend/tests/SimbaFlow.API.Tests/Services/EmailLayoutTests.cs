using FluentAssertions;
using SimbaFlow.Application.Common.Email;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The one layout every email is drawn in. These pin what the reader is promised whatever the
/// email is about: who it greets, that the button can always be reached, and that nothing a person
/// typed can turn into markup.
/// </summary>
public class EmailLayoutTests
{
    private static string Render(
        string? greeting = null, EmailButton? button = null, string? note = null, string title = "Reset your password") =>
        EmailLayout.Render(new EmailContent(
            Kicker: "Account security",
            Title: title,
            BodyHtml: EmailLayout.Paragraph("Body text."),
            Greeting: greeting,
            Button: button,
            NoteHtml: note));

    [Fact]
    public void APersonIsGreetedByName()
    {
        Render(greeting: EmailLayout.Greeting("Mama")).Should().Contain("Hello Mama,");
    }

    [Theory]
    [InlineData("System")]
    [InlineData("administrator")]
    [InlineData("  ")]
    [InlineData(null)]
    public void APlaceholderNameIsNotGreetedAsIfItWereAPerson(string? firstName)
    {
        // The platform's first account is "System Administrator"; its reset read "Hello System,".
        EmailLayout.Greeting(firstName).Should().BeNull();
        Render(greeting: EmailLayout.Greeting(firstName)).Should().Contain("Hello,").And.NotContain("Hello System");
    }

    [Fact]
    public void TheButtonsLinkIsAlsoWrittenOutForClientsThatDropButtons()
    {
        const string url = "https://app.laba.et/reset-password?email=a%40b.c&token=xyz";
        var html = Render(button: new EmailButton("Choose a new password", url));

        html.Should().Contain("Choose a new password");
        html.Should().Contain("Button not working?");
        // Once in the button, once in the fallback link's href, once as its visible text.
        CountOf(html, "reset-password?email=a%40b.c&amp;token=xyz").Should().Be(3);
    }

    [Fact]
    public void AnEmailWithNoButtonOffersNoLinkToCopy()
    {
        Render().Should().NotContain("Button not working?");
    }

    [Fact]
    public void TheNoteAppearsOnlyWhenThereIsOne()
    {
        Render(note: "<b>Didn't ask for this?</b> Ignore it.").Should().Contain("Didn't ask for this?");
        Render().Should().NotContain("background:#f4f8f5");
    }

    [Fact]
    public void ATitleOrNameCannotInjectMarkup()
    {
        var html = Render(title: "<script>alert(1)</script>", greeting: "<img src=x onerror=alert(1)>");

        html.Should().NotContain("<script>").And.NotContain("<img");
        html.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void EveryEmailCarriesTheSameBannerAndFooter()
    {
        var html = Render();

        html.Should().StartWith("<!doctype html>");
        html.Should().Contain("Account security");
        html.Should().Contain("Labour export agency management");
        html.Should().Contain("Reply to this email");
    }

    private static int CountOf(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + 1, StringComparison.Ordinal))
            count++;
        return count;
    }
}
