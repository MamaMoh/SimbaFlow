namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Marker interface for commands/queries that require office-level access control.
/// The WorkflowAuthorizationBehavior checks that the user belongs to the target office.
/// Agency Owners bypass office checks (they have access to all offices in their tenant).
/// </summary>
public interface IRequireOfficeAccess
{
    /// <summary>The office ID that the operation targets. Null means no office restriction.</summary>
    Guid? TargetOfficeId { get; }
}
