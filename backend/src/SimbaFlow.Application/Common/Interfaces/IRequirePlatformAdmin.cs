namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Work that belongs to the platform team, not to any agency.
///
/// A permission is the wrong gate for this. Permissions are granted by whoever administers an
/// agency, so "only the platform may do this" expressed as a permission is only true until
/// somebody grants it — and the role catalogue is exactly the screen where that granting
/// happens, which made it a lock whose key sat inside the room.
///
/// Checked before the permission checks and never waived, so an agency account cannot reach
/// these however its roles are configured.
/// </summary>
public interface IRequirePlatformAdmin
{
}
