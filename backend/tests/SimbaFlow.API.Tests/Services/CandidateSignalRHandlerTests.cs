using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SimbaFlow.Domain.Events;
using SimbaFlow.Infrastructure.DomainEvents;
using SimbaFlow.Infrastructure.RealTime;
using SimbaFlow.Infrastructure.Services.Bot;

namespace SimbaFlow.API.Tests.Services;

public class CandidateSignalRHandlerTests
{
    [Fact]
    public async Task StageChangedHandler_BroadcastsToTenantOffice()
    {
        var broadcaster = Substitute.For<ISignalRBroadcaster>();
        var push = Substitute.For<INotificationPushService>();
        var handler = new CandidateStageChangedHandler(
            broadcaster, push, NullLogger<CandidateStageChangedHandler>.Instance);

        var tenantId = Guid.NewGuid();
        var evt = new CandidateStageChangedEvent(
            Guid.NewGuid(), "Ada Lovelace", tenantId,
            Guid.NewGuid(), "Intake", Guid.NewGuid(), "Embassy", "tester");

        await handler.Handle(evt, CancellationToken.None);

        await broadcaster.Received(1).BroadcastCandidateUpdateAsync(
            tenantId,
            Arg.Is<CandidateUpdatedMessage>(m =>
                m.ChangeType == "StageTransitioned"
                && m.Field == "currentStage"
                && m.OldValue == "Intake"
                && m.NewValue == "Embassy"));

        await push.Received(1).PushStageChangedAsync(
            tenantId, "Ada Lovelace", "Embassy", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StatusChangedHandler_BroadcastsFieldUpdate()
    {
        var broadcaster = Substitute.For<ISignalRBroadcaster>();
        var handler = new CandidateStatusChangedHandler(
            broadcaster, NullLogger<CandidateStatusChangedHandler>.Instance);

        var tenantId = Guid.NewGuid();
        var evt = new CandidateStatusChangedEvent(
            Guid.NewGuid(), "Ada Lovelace", tenantId,
            "medical", "Pending", "Fit", "tester");

        await handler.Handle(evt, CancellationToken.None);

        await broadcaster.Received(1).BroadcastCandidateUpdateAsync(
            tenantId,
            Arg.Is<CandidateUpdatedMessage>(m =>
                m.ChangeType == "StatusUpdated"
                && m.Field == "medical"
                && m.OldValue == "Pending"
                && m.NewValue == "Fit"));
    }

    [Fact]
    public async Task StageChangedHandler_SkipsWhenTenantEmpty()
    {
        var broadcaster = Substitute.For<ISignalRBroadcaster>();
        var push = Substitute.For<INotificationPushService>();
        var handler = new CandidateStageChangedHandler(
            broadcaster, push, NullLogger<CandidateStageChangedHandler>.Instance);

        var evt = new CandidateStageChangedEvent(
            Guid.NewGuid(), "X", Guid.Empty,
            null, null, Guid.NewGuid(), "Embassy", "tester");

        await handler.Handle(evt, CancellationToken.None);

        await broadcaster.DidNotReceiveWithAnyArgs()
            .BroadcastCandidateUpdateAsync(default, default!);
        await push.DidNotReceiveWithAnyArgs()
            .PushStageChangedAsync(default, default!, default, default);
    }
}
