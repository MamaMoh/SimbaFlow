using System.Reflection;
using FluentAssertions;
using SimbaFlow.Infrastructure.Persistence.Seeds;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// What each role is actually allowed to do.
///
/// The screen decides which buttons to draw from these grants, so a role that quietly holds too
/// much is an action offered to the wrong desk, and one that quietly holds too little is a button
/// that vanishes for the person whose job it is. Both are invisible until someone signs in as that
/// role and tries, which is why they are pinned here.
/// </summary>
public class RolePermissionMapTests
{
    private static readonly Dictionary<string, string[]> Roles = ReadPrivate<Dictionary<string, string[]>>(
        typeof(RolePermissionSeeder), "RolePermissionMap");

    private static readonly HashSet<string> KnownCodes =
        ReadPrivate<(string Module, string Code, string Name)[]>(typeof(PermissionSeeder), "Permissions")
            .Select(p => p.Code)
            .ToHashSet();

    private static T ReadPrivate<T>(Type type, string field) =>
        (T)type.GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Fact]
    public void EveryGrantedCodeActuallyExists()
    {
        // The seeder skips a code it cannot find, so a typo does not fail — it silently leaves the
        // role one permission short, and the button for it never appears for anyone.
        var unknown = Roles
            .SelectMany(r => r.Value.Select(code => $"{r.Key} → {code}"))
            .Where(pair => !KnownCodes.Contains(pair.Split(" → ")[1]))
            .ToList();

        unknown.Should().BeEmpty("a permission that is not in PermissionSeeder is never granted");
    }

    [Fact]
    public void NoRoleIsGrantedTheSamePermissionTwice()
    {
        foreach (var (role, codes) in Roles)
            codes.Should().OnlyHaveUniqueItems($"{role} lists a permission more than once");
    }

    [Theory]
    // Reading a board and moving a candidate along it are different jobs.
    [InlineData("Auditor", "workflow.execute")]
    [InlineData("DataEntryClerk", "workflow.execute")]
    [InlineData("CaseExecutive", "workflow.execute")]
    [InlineData("FinanceOfficer", "workflow.execute")]
    // Removing a person from the system belongs to whoever owns the agency.
    [InlineData("Auditor", "candidate.delete")]
    [InlineData("DataEntryClerk", "candidate.delete")]
    [InlineData("FieldAgent", "candidate.delete")]
    [InlineData("OfficeManager", "candidate.delete")]
    // Amending records is not something a reader does.
    [InlineData("Auditor", "candidate.update")]
    [InlineData("CaseExecutive", "candidate.update")]
    [InlineData("FinanceOfficer", "candidate.update")]
    // Accounts are administered from one desk, not by everyone who can list staff.
    [InlineData("OfficeManager", "users.write")]
    [InlineData("Auditor", "users.write")]
    // The platform administrator deliberately cannot read any agency's people.
    [InlineData("PlatformAdmin", "candidate.read")]
    public void RoleDoesNotHold(string role, string permission)
    {
        Roles[role].Should().NotContain(permission);
    }

    [Theory]
    // The desks that do the work still have to be able to do it.
    [InlineData("EmbassyOfficer", "workflow.execute")]
    [InlineData("EmbassyOfficer", "lmis.document")]
    [InlineData("FieldAgent", "workflow.execute")]
    [InlineData("FieldAgent", "candidate.update")]
    [InlineData("DataEntryClerk", "candidate.create")]
    [InlineData("AgencyOwner", "candidate.delete")]
    [InlineData("AgencyOwner", "users.write")]
    [InlineData("PlatformAdmin", "users.write")]
    public void RoleHolds(string role, string permission)
    {
        Roles[role].Should().Contain(permission);
    }

    [Fact]
    public void EveryRoleThatReachesABoardCanAlsoReadTheCandidatesOnIt()
    {
        // The boards' row menus link to the candidate's page and offer their paperwork, both read
        // under candidate.read. A role with a board and no candidate.read gets a menu with the
        // middle cut out of it.
        string[] boards = ["embassy.read", "lmis.read", "travel.read", "arrival.read", "embassy.case_view"];

        foreach (var (role, codes) in Roles)
        {
            if (!codes.Intersect(boards).Any()) continue;
            codes.Should().Contain("candidate.read", $"{role} works a board");
        }
    }
}
