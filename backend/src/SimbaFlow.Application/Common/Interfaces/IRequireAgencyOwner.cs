namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Work that changes how an agency runs, rather than work done inside it.
///
/// The pipeline every candidate is moved through is the agency's own — which stage comes next,
/// what has to be filled in before a step is allowed, which desk sees whom. An agency may shape
/// it however it works; that is the whole point of it being configurable. But a coordinator who
/// can move a candidate to the Embassy stage has no business deciding that the Embassy stage no
/// longer requires a medical, and somebody rearranging the pipeline while forty people are
/// standing in it is not an edit anyone can undo from a board.
///
/// So this is the agency owner's, and the platform team's, and nobody else's.
///
/// A permission would be the wrong gate for the same reason it was wrong for roles: permissions
/// are granted inside the agency, so "only the owner may do this" expressed as a permission
/// lasts exactly until somebody grants it. Checked before the permission checks and waived only
/// for the platform team.
/// </summary>
public interface IRequireAgencyOwner
{
    /// <summary>
    /// What to tell someone who is refused. Overridden where the default would leave them
    /// guessing which thing they were not allowed to change.
    /// </summary>
    string OwnerOnlyMessage =>
        "This is set up by the agency owner or the platform team.";
}
