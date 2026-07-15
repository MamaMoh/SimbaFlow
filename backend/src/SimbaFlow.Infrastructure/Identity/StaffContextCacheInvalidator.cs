using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SimbaFlow.Domain.Entities.Staff;

namespace SimbaFlow.Infrastructure.Identity;

/// <summary>
/// Listens to domain events that change staff context and invalidates the memory cache.
/// Ensures the next request resolves fresh data from the database.
/// 
/// In a multi-instance deployment, replace IMemoryCache with Redis pub/sub
/// or a distributed cache invalidation channel.
/// </summary>
public class StaffContextCacheInvalidator :
    INotificationHandler<StaffTerminatedEvent>,
    INotificationHandler<StaffSuspendedEvent>,
    INotificationHandler<UserAccountProvisionedEvent>,
    INotificationHandler<ClinicalPrivilegesAssignedEvent>,
    INotificationHandler<StaffIdentifierVerifiedEvent>
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<StaffContextCacheInvalidator> _logger;

    public StaffContextCacheInvalidator(IMemoryCache cache, ILogger<StaffContextCacheInvalidator> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task Handle(StaffTerminatedEvent notification, CancellationToken cancellationToken)
    {
        EvictStaffContext(notification.StaffProfileId, notification.UserId);
        return Task.CompletedTask;
    }

    public Task Handle(StaffSuspendedEvent notification, CancellationToken cancellationToken)
    {
        EvictStaffContext(notification.StaffProfileId, notification.UserId);
        return Task.CompletedTask;
    }

    public Task Handle(UserAccountProvisionedEvent notification, CancellationToken cancellationToken)
    {
        EvictStaffContext(notification.StaffProfileId, notification.UserId);
        return Task.CompletedTask;
    }

    public Task Handle(ClinicalPrivilegesAssignedEvent notification, CancellationToken cancellationToken)
    {
        EvictStaffContext(notification.StaffProfileId, null);
        return Task.CompletedTask;
    }

    public Task Handle(StaffIdentifierVerifiedEvent notification, CancellationToken cancellationToken)
    {
        EvictStaffContext(notification.StaffProfileId, null);
        return Task.CompletedTask;
    }

    private void EvictStaffContext(Guid staffProfileId, Guid? userId)
    {
        // Evict the profile-keyed cache entry
        var profileKey = $"staff_context:{staffProfileId}";
        _cache.Remove(profileKey);

        // Evict the user-keyed lookup cache (if user is linked)
        if (userId.HasValue)
        {
            var userKey = $"staff_context_user:{userId.Value}";
            _cache.Remove(userKey);
        }

        _logger.LogDebug(
            "Evicted staff context cache for StaffProfileId={StaffProfileId}, UserId={UserId}",
            staffProfileId, userId);
    }
}
