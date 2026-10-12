namespace SimbaFlow.Domain.Services;

/// <summary>
/// The roles that answer for an agency rather than work inside it.
///
/// One account per agency holds this: the admin created when the agency is provisioned. It is
/// not a permission and cannot be granted from the roles screen, which is the point — see
/// <c>IRequireAgencyOwner</c> for what that distinction is protecting.
///
/// Named here rather than spelled as a literal at each site, because the places that test for it
/// — the authorization pipeline, the workflow setup screen, the seeded permission sets — have to
/// agree, and three copies of a string do not stay equal.
/// </summary>
public static class AgencyRoles
{
    public const string Owner = "AgencyOwner";

    public static bool IsOwner(string? roleName) =>
        string.Equals(roleName, Owner, StringComparison.Ordinal);

    /// <summary>Whether any of these role names is the agency's owner.</summary>
    public static bool Owns(IEnumerable<string>? roles) =>
        roles is not null && roles.Any(IsOwner);
}
