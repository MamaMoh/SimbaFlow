namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Marker interface for commands/queries that require a specific permission.
/// The AuthorizationBehavior checks this before handler execution.
/// SuperAdmin bypasses all permission checks.
/// </summary>
public interface IRequirePermission
{
    /// <summary>Permission code required (e.g., "candidate.read", "workflow.execute").</summary>
    string RequiredPermission { get; }
}
