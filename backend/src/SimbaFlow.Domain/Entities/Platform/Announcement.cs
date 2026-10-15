using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Platform;

/// <summary>How loudly an announcement is shown.</summary>
public enum AnnouncementLevel
{
    Info = 0,
    Warning = 1,
    Critical = 2,
}

/// <summary>
/// A message from the platform team shown as a banner to agency staff: a maintenance window,
/// a new feature, a policy change.
///
/// Addressed to every agency, or to one. Shown between <see cref="StartsAt"/> and
/// <see cref="EndsAt"/>; people can dismiss it for themselves, which is remembered in their browser.
/// </summary>
public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public AnnouncementLevel Level { get; set; } = AnnouncementLevel.Info;

    /// <summary>One agency, or null for all of them.</summary>
    public Guid? TenantId { get; set; }

    public DateTime StartsAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null keeps it up until someone takes it down.</summary>
    public DateTime? EndsAt { get; set; }
}
