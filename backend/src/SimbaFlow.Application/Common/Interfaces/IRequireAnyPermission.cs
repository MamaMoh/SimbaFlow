namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Marker for commands/queries that more than one permission can unlock.
///
/// Most actions belong to exactly one desk and <see cref="IRequirePermission"/> says so. A few are
/// reached from several — filing a document is done by the clerk who owns the candidate's record
/// and by the officer who owns the LMIS board, and neither holds the other's permission. Naming one
/// of them would lock the other out of work they are supposed to do.
///
/// Holding any one of <see cref="AcceptedPermissions"/> is enough. SuperAdmin bypasses the check.
/// </summary>
public interface IRequireAnyPermission
{
    /// <summary>Permission codes, any one of which admits the caller.</summary>
    IReadOnlyList<string> AcceptedPermissions { get; }
}
