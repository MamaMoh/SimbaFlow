using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Services;

/// <summary>
/// Unit 4 stub — Mark Notified is status-only; bot delivery is Unit 7.
/// </summary>
public sealed class NoOpCandidateNotifier : ICandidateNotifier
{
    private readonly ILogger<NoOpCandidateNotifier> _logger;

    public NoOpCandidateNotifier(ILogger<NoOpCandidateNotifier> logger)
    {
        _logger = logger;
    }

    public Task NotifyAsync(Guid candidateId, string messageKey, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "NoOp notify candidate {CandidateId} key {MessageKey} (bot deferred to Unit 7)",
            candidateId,
            messageKey);
        return Task.CompletedTask;
    }
}
