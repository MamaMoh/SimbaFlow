using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Every permission a command demands has to be one the seeder actually creates.
///
/// A typo here does not fail a build or a request in development as a super admin — it produces a
/// 403 for every real user, on an endpoint that looks finished. Two shipped that way in one
/// sitting ("workflow.transition" for a permission named workflow.execute, and settings.write,
/// which no role held), so the wiring is checked rather than trusted.
/// </summary>
public class PermissionWiringTests
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Directory.GetParent(dir)?.FullName;
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    [Fact]
    public void EveryRequiredPermissionIsSeeded()
    {
        var seeder = File.ReadAllText(RepoFile(
            "src", "SimbaFlow.Infrastructure", "Persistence", "Seeds", "PermissionSeeder.cs"));
        var seeded = Regex.Matches(seeder, "\"([a-z_]+\\.[a-z_.]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

        var apiRoot = RepoFile("src", "SimbaFlow.API");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in Regex.Matches(
                File.ReadAllText(file), "RequiredPermission\\s*=>\\s*\"([^\"]+)\""))
            {
                var code = m.Groups[1].Value;
                if (!seeded.Contains(code))
                    offenders.Add($"{Path.GetFileName(file)} demands '{code}'");
            }
        }

        offenders.Should().BeEmpty(
            "a command demanding an unseeded permission is a 403 for every user who is not a super admin");
    }
}
