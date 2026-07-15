using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.Infrastructure.Audit;

/// <summary>
/// Background service that drains the read-audit channel and bulk-inserts to PostgreSQL.
/// Writes in batches of up to 1000 entries or every 5 seconds, whichever comes first.
/// </summary>
public class ReadAuditBatchWriter : BackgroundService
{
    private readonly Channel<ReadAuditEntry> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReadAuditBatchWriter> _logger;

    private const int BatchSize = 1000;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    public ReadAuditBatchWriter(
        Channel<ReadAuditEntry> channel,
        IServiceProvider serviceProvider,
        ILogger<ReadAuditBatchWriter> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<ReadAuditLog>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for the first item or timeout
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(FlushInterval);

                try
                {
                    while (batch.Count < BatchSize)
                    {
                        var entry = await _channel.Reader.ReadAsync(cts.Token);
                        batch.Add(MapToLog(entry));
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Flush timeout — write whatever we have
                }

                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch, stoppingToken);
                    batch.Clear();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down — flush remaining
                while (_channel.Reader.TryRead(out var entry))
                    batch.Add(MapToLog(entry));

                if (batch.Count > 0)
                    await FlushBatchAsync(batch, CancellationToken.None);

                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReadAuditBatchWriter: Error processing batch of {Count} entries", batch.Count);
                batch.Clear();
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task FlushBatchAsync(List<ReadAuditLog> batch, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            context.ReadAuditLogs.AddRange(batch);
            await context.SaveChangesAsync(ct);

            _logger.LogDebug("ReadAuditBatchWriter: Flushed {Count} read-audit entries", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReadAuditBatchWriter: Failed to flush {Count} entries to database", batch.Count);
        }
    }

    private static ReadAuditLog MapToLog(ReadAuditEntry entry) => new()
    {
        UserId = entry.UserId,
        EntityType = entry.EntityType,
        EntityId = entry.EntityId,
        Action = entry.Action,
        AccessedAt = DateTime.UtcNow,
        IpAddress = entry.IpAddress,
        UserAgent = entry.UserAgent,
        IsBreakTheGlass = entry.IsBreakTheGlass,
        Reason = entry.Reason,
    };
}
