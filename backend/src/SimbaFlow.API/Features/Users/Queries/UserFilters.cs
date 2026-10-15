using SimbaFlow.Application.Common.Filtering;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Users.Queries;

/// <summary>
/// Which columns of the staff list may be filtered, and against what.
///
/// Deliberately short. Most of what is on an account — the password hash, the lockout counters,
/// the security stamp — has no business being asked about from a list, and a whitelist is the
/// only thing standing between a field name typed into a query string and a column. What is
/// here is what the board shows.
///
/// Agency is absent even though the column exists: the name is held on the tenant, not on the
/// account, and a funnel that filtered nothing would be worse than no funnel. Roles likewise
/// come from Identity's own join and are read per user after the page is chosen.
/// </summary>
public static class UserFilters
{
    public static FilterableColumns<ApplicationUser> For() =>
        new FilterableColumns<ApplicationUser>()
            .Text("username", u => u.UserName)
            .Custom("fullName", f => FilterPredicates.Text<ApplicationUser>(
                u => u.FirstName + " " + u.LastName, f))
            .Text("email", u => u.Email)
            .Custom("isActive", f => FilterPredicates.Text<ApplicationUser>(
                // Shown as Active or Inactive, so that is what it is matched against.
                u => u.IsActive ? "active" : "inactive", f))
            .Timestamp("lastLoginAt", u => u.LastLoginAt);
}
