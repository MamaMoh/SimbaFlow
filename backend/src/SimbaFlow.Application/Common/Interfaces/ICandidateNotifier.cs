namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Candidate messaging (Telegram/WhatsApp). Unit 4 uses NoOp; Unit 7 provides real delivery.
/// </summary>
public interface ICandidateNotifier
{
    Task NotifyAsync(Guid candidateId, string messageKey, CancellationToken cancellationToken = default);
}
