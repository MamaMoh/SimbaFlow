using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SimbaFlow.Application.Common.Exceptions;
using SimbaFlow.Infrastructure.Options;
using SimbaFlow.Infrastructure.Services.Bot;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The bot has to stay up. One person's command failing must not wedge the poller for everyone,
/// and when the reason for a failure is already known it belongs in the reply rather than behind
/// a generic apology the user can only respond to by trying again.
/// </summary>
public class TelegramPollingServiceTests
{
    private readonly ITelegramGateway _telegram = Substitute.For<ITelegramGateway>();
    private readonly ITelegramCommandDispatcher _dispatcher = Substitute.For<ITelegramCommandDispatcher>();
    private readonly TelegramPollerState _state = new();

    private readonly List<(string ChatId, string Text)> _sent = [];

    public TelegramPollingServiceTests()
    {
        _telegram.IsConfigured.Returns(true);
        _telegram.GetMeAsync(Arg.Any<CancellationToken>())
            .Returns(new TelegramBotIdentity(1, "Test Bot", "testbot"));
        _telegram.SendMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _sent.Add((call.ArgAt<string>(0), call.ArgAt<string>(1)));
                return Task.FromResult<string?>("ok");
            });
    }

    /// <summary>Runs the loop until it has drained the queued updates, then stops it.</summary>
    private async Task RunUntilDrainedAsync(params TelegramUpdate[] updates)
    {
        var remaining = new Queue<TelegramUpdate>(updates);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        _telegram.GetUpdatesAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (remaining.Count == 0)
                {
                    cts.Cancel();
                    return Task.FromResult<IReadOnlyList<TelegramUpdate>>([]);
                }
                return Task.FromResult<IReadOnlyList<TelegramUpdate>>([remaining.Dequeue()]);
            });

        var services = new ServiceCollection();
        services.AddSingleton(_dispatcher);
        var provider = services.BuildServiceProvider();

        var options = Substitute.For<IOptionsMonitor<TelegramOptions>>();
        options.CurrentValue.Returns(new TelegramOptions { PollingEnabled = true, LongPollTimeoutSeconds = 0 });

        var service = new TelegramPollingService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            _telegram,
            _state,
            Substitute.For<ILogger<TelegramPollingService>>());

        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(300, CancellationToken.None);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task AnUnavailableTenantIsExplained_NotHiddenBehindTheGenericApology()
    {
        _dispatcher
            .When(d => d.HandleAsync(Arg.Any<TelegramUpdate>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new TenantUnavailableException(
                "Your agency is not available — it may have been removed or suspended."));

        await RunUntilDrainedAsync(new TelegramUpdate(1, "chat-1", "/stats", "someone"));

        _sent.Should().ContainSingle();
        _sent[0].Text.Should().Contain("not available");
        _sent[0].Text.Should().NotContain("that command failed",
            "the reason is known, so retrying is not the advice this person needs");
    }

    [Fact]
    public async Task AnUnexpectedFailureStillApologises()
    {
        _dispatcher
            .When(d => d.HandleAsync(Arg.Any<TelegramUpdate>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("something we did not anticipate"));

        await RunUntilDrainedAsync(new TelegramUpdate(1, "chat-1", "/stats", "someone"));

        _sent.Should().ContainSingle();
        _sent[0].Text.Should().Contain("that command failed");
        _sent[0].Text.Should().NotContain("something we did not anticipate",
            "internal detail is not for the person typing the command");
    }

    [Fact]
    public async Task OnePersonsFailingCommandDoesNotWedgeTheBotForEveryoneElse()
    {
        _dispatcher
            .When(d => d.HandleAsync(
                Arg.Is<TelegramUpdate>(u => u.ChatId == "chat-broken"), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("poisoned update"));

        await RunUntilDrainedAsync(
            new TelegramUpdate(1, "chat-broken", "/stats", "a"),
            new TelegramUpdate(2, "chat-fine", "/help", "b"));

        // The offset must move past the failure, or Telegram redelivers it forever and every
        // other user is stuck behind it.
        _state.LastUpdateId.Should().Be(2);
        await _dispatcher.Received(1).HandleAsync(
            Arg.Is<TelegramUpdate>(u => u.ChatId == "chat-fine"), Arg.Any<CancellationToken>());
    }
}
