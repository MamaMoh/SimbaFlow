using FluentAssertions;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Per-agency step ownership: a transition's AllowedRoles decides who may perform
/// that step. Empty = anyone with stage access, so one agency can give several
/// steps to a single role while another splits them across roles.
///
/// A role block is not actionable by the user, so such steps are hidden from the
/// action list entirely (rather than shown disabled like condition/field blocks).
/// </summary>
public class WorkflowRoleGatingTests
{
    private static bool RoleAllows(string[] allowedRoles, params string[] userRoles) =>
        allowedRoles.Length == 0 ||
        allowedRoles.Any(r => userRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void EmptyAllowedRoles_AllowsAnyone()
    {
        RoleAllows([], "FieldAgent").Should().BeTrue();
        RoleAllows([], "EmbassyOfficer").Should().BeTrue();
    }

    [Fact]
    public void RestrictedStep_OnlyMatchingRolePasses()
    {
        string[] allowed = ["OfficeManager"];
        RoleAllows(allowed, "OfficeManager").Should().BeTrue();
        RoleAllows(allowed, "FieldAgent").Should().BeFalse();
        RoleAllows(allowed, "EmbassyOfficer").Should().BeFalse();
    }

    [Fact]
    public void MultipleRoles_AnyMatchPasses()
    {
        string[] allowed = ["EmbassyOfficer", "CaseExecutive"];
        RoleAllows(allowed, "CaseExecutive").Should().BeTrue();
        RoleAllows(allowed, "FinanceOfficer").Should().BeFalse();
    }

    [Fact]
    public void RoleMatching_IsCaseInsensitive()
    {
        RoleAllows(["officemanager"], "OfficeManager").Should().BeTrue();
    }

    [Fact]
    public void UserWithSeveralRoles_PassesIfAnyMatches()
    {
        // One person owning several steps in a small agency.
        RoleAllows(["FinanceOfficer"], "FieldAgent", "FinanceOfficer").Should().BeTrue();
    }
}
