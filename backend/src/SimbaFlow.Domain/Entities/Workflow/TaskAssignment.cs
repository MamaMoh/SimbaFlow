using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Workflow;

/// <summary>
/// Maps a staff user to ownership of a workflow task column (track).
/// </summary>
public class TaskAssignment : BaseEntity
{
    public Guid StaffUserId { get; set; }
    public string TrackKey { get; set; } = string.Empty;
}
