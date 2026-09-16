using System.Text.RegularExpressions;
using FluentAssertions;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Controls that are easy to write and easy to lose again.
///
/// Every one of these was found switched off in a review rather than by a failing test, because
/// nothing about the running system looks wrong when a limiter is configured but never attached, or
/// when a route is written inline and so never meets the permission pipeline. The source is checked
/// in the same spirit as <see cref="PermissionWiringTests"/>.
/// </summary>
public class SecurityWiringTests
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Directory.GetParent(dir)?.FullName;
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static string Read(params string[] parts) => File.ReadAllText(RepoFile(parts));

    // ──── Secrets ────

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void NoConnectionStringPasswordIsCommitted(string file)
    {
        var text = Read("src", "SimbaFlow.API", file);

        Regex.Match(text, @"Password=(?<value>[^""';]+)").Groups["value"].Value
            .Should().BeEmpty(
                "{0} is tracked in git, so a password here is published to everyone with the "
                + "source — and stays in history after it is removed", file);
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void NoSigningKeyIsCommitted(string file)
    {
        var text = Read("src", "SimbaFlow.API", file);

        Regex.Match(text, @"""Key""\s*:\s*""(?<value>[^""]*)""").Groups["value"].Value
            .Should().BeEmpty(
                "a committed Jwt:Key lets anyone with the repository forge a token this API accepts");
    }

    [Fact]
    public void TheBootstrapAdminHasNoPasswordWrittenIntoTheSource()
    {
        var seeder = Read(
            "src", "SimbaFlow.Infrastructure", "Persistence", "Seeds", "AdminSeeder.cs");

        // The seeder runs unconditionally at startup, production included. A literal here is a
        // standing back door, published with the source, that nothing ever prompts anyone to change.
        Regex.IsMatch(seeder, @"const\s+string\s+\w*[Pp]assword\s*=").Should().BeFalse();

        seeder.Should().Contain("Seed:AdminPassword",
            "the password has to come from configuration");
        seeder.Should().Contain("MustChangePassword = true",
            "whoever ends up with the bootstrap account proves themselves by choosing a new password");
    }

    // ──── Rate limiting ────

    [Theory]
    [InlineData("/login")]
    [InlineData("/login/mfa")]
    [InlineData("/refresh")]
    [InlineData("/forgot-password")]
    [InlineData("/reset-password")]
    public void EveryUnauthenticatedAuthRouteIsRateLimited(string route)
    {
        var module = Read("src", "SimbaFlow.API", "Features", "Auth", "AuthModule.cs");

        var mapping = Regex.Match(
            module,
            $@"MapPost\(""{Regex.Escape(route)}"".*?\}}\)(?<modifiers>[^;]*);",
            RegexOptions.Singleline);

        mapping.Success.Should().BeTrue("{0} should still be mapped", route);
        mapping.Groups["modifiers"].Value.Should().Contain("RequireRateLimiting",
            "a limiter that is configured but never attached is dead configuration — "
            + "{0} was left open to unthrottled guessing", route);
    }

    [Fact]
    public void TheLimitersAreKeyedPerCaller()
    {
        var extensions = Read("src", "SimbaFlow.API", "Extensions", "ServiceExtensions.cs");

        foreach (var policy in new[] { "login", "auth", "refresh" })
        {
            extensions.Should().Contain($"AddPolicy(\"{policy}\", ClientPartition",
                "a single window shared by every caller means one client can exhaust it for "
                + "everyone, and spraying one password across many accounts is barely slowed");
        }
    }

    // ──── The permission pipeline ────

    [Fact]
    public void RoleAdministrationGoesThroughTheMediatorPipeline()
    {
        var module = Read("src", "SimbaFlow.API", "Features", "Roles", "RoleModule.cs");

        // Written inline against a DbContext, these routes never reach AuthorizationBehavior — so
        // any signed-in user could create roles, rewrite an existing role's permissions, and delete
        // them. The pipeline is the only thing that enforces IRequirePermission.
        module.Should().NotContain("ITenantDbContext",
            "role routes must dispatch through ISender, not touch a DbContext directly");
        module.Should().NotContain("IPlatformDbContext");

        Regex.Matches(module, @"Map(Get|Post|Put|Delete)\(").Should().NotBeEmpty();
        foreach (Match route in Regex.Matches(module, @"Map(?:Get|Post|Put|Delete)\([^;]*?;", RegexOptions.Singleline))
        {
            route.Value.Should().Contain("sender.Send",
                "every role route has to go through the pipeline that checks permissions");
        }
    }

    [Fact]
    public void CandidateDocumentsAreNotServedStraightFromTheDbContext()
    {
        var module = Read("src", "SimbaFlow.API", "Features", "Candidates", "CandidateModule.cs");

        // Downloading a document was written inline and checked only that the caller was signed in,
        // so any user in the agency could pull any candidate's passport scan given two ids.
        module.Should().NotContain("context.CandidateDocuments",
            "document access must run through a query that declares the permission it needs");
    }

    // ──── Tenancy ────

    [Fact]
    public void TheTenantInterceptorNeverLeavesTheSchemaInherited()
    {
        var interceptor = Read(
            "src", "SimbaFlow.Infrastructure", "Persistence", "TenantConnectionInterceptor.cs");

        // Connections are pooled. Skipping the SET does not mean "no schema" — it means the session
        // keeps whichever schema the last borrower of that connection was pointed at.
        interceptor.Should().NotContain("if (!string.IsNullOrWhiteSpace(schemaName))",
            "the SET must happen on every path, not only when a schema was resolved");
        interceptor.Should().Contain("EscapeIdent(schemaName)",
            "the schema name is interpolated into SQL as an identifier and cannot be parameterised");
    }
}
