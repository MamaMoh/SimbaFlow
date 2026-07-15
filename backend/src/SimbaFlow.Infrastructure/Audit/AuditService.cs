using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Audit;

/// <summary>
/// Persists audit log entries to the AuditLog table.
/// Designed for append-only writes (HIPAA compliance).
/// </summary>
public class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public AuditService(ILogger<AuditService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        try
        {
            // Use a separate scope to avoid interfering with the current unit of work
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Persistence.ApplicationDbContext>();

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = entry.UserId,
                Action = entry.Action,
                EntityType = entry.EntityType,
                EntityId = entry.EntityId,
                Success = entry.Success,
                DurationMs = entry.DurationMs,
                OldValues = entry.OldValues,
                NewValues = entry.NewValues,
                IpAddress = entry.IpAddress,
                UserAgent = entry.UserAgent,
                Timestamp = DateTime.UtcNow,
            });

            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit failures should never break the main flow
            _logger.LogError(ex, "Failed to write audit log for action {Action} by user {UserId}",
                entry.Action, entry.UserId);
        }
    }
}

/// <summary>
/// Audit log database entity. Append-only table for compliance.
/// Records both the acting user and (if impersonated) the admin who initiated the session.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user whose context the action was performed in.</summary>
    public string? UserId { get; set; }

    /// <summary>
    /// If the session was impersonated, the admin who initiated it.
    /// Audit trail MUST show: "Action by {UserId} (Impersonated by {ImpersonatedByUserId})".
    /// </summary>
    public string? ImpersonatedByUserId { get; set; }

    /// <summary>The StaffProfile ID of the acting user (for clinical audit correlation).</summary>
    public string? StaffProfileId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int DurationMs { get; set; }
    public bool Success { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
