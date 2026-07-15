using System.Threading.Channels;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Audit;

/// <summary>
/// Non-blocking read audit service using System.Threading.Channels.
/// Publishes access events to an unbounded channel; a background writer drains them in batches.
/// </summary>
public class ReadAuditService : IReadAuditService
{
    private readonly Channel<ReadAuditEntry> _channel;

    public ReadAuditService(Channel<ReadAuditEntry> channel)
    {
        _channel = channel;
    }

    /// <summary>
    /// Fire-and-forget publish. Never blocks the caller.
    /// If the channel is full (shouldn't happen with unbounded), the event is dropped silently.
    /// </summary>
    public void LogAccess(ReadAuditEntry entry)
    {
        _channel.Writer.TryWrite(entry);
    }
}
