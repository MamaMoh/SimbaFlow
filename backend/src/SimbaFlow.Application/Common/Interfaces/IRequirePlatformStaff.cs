namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Work the whole platform team may do, the support desk included: looking at agencies and
/// helping them, as opposed to changing what an agency is or what it pays
/// (<see cref="IRequirePlatformAdmin"/>).
///
/// Like that gate it is keyed on the role, never on a permission, and checked with no bypass.
/// </summary>
public interface IRequirePlatformStaff
{
}
