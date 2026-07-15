using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that archives old user sessions daily.
/// Marks sessions inactive and can be extended for archival to cold storage.
/// </summary>
public class SessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public SessionCleanupService(IServiceProvider serviceProvider, ILogger<SessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var cutoff = DateTime.UtcNow.AddDays(-90);

                // Mark old active sessions as inactive (stale sessions)
                var staleUpdated = await context.UserSessions
                    .Where(s => s.IsActive && s.LastActivityAt < DateTime.UtcNow.AddDays(-7))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsActive, false)
                        .SetProperty(x => x.LogoutAt, DateTime.UtcNow), stoppingToken);

                // Delete very old sessions (90+ days)
                var deleted = await context.UserSessions
                    .Where(s => !s.IsActive && s.LoginAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (staleUpdated > 0 || deleted > 0)
                    _logger.LogInformation(
                        "SessionCleanup: Marked {StaleCount} stale sessions inactive, deleted {DeletedCount} old sessions",
                        staleUpdated, deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "SessionCleanup: Error during cleanup");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
