using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Workflow;

/// <summary>
/// Restricts which role/permission may set a track to a specific status value
/// (e.g. embassy → Submitted requires CaseExecutive).
/// </summary>
public class StatusTransitionPermission : BaseEntity
{
    public string TrackKey { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string AllowedRoleCode { get; set; } = string.Empty;
    public string? AllowedPermissionCode { get; set; }
}