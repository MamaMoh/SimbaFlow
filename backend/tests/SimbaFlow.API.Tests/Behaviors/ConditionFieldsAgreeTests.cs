using System.Text.RegularExpressions;
using FluentAssertions;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The list of fields a condition can be written about exists twice, and the two copies have to
/// agree.
///
/// The engine answers a condition from two places: the tracks a candidate carries, and the
/// handful of fields in BuildCandidateFields. The setup screen offers that same list in a
/// dropdown, so nobody has to know that the passport is `passportNumber` and not `passport`.
///
/// A field in one list and not the other is the worst kind of wrong: a condition written on it
/// reads as empty, so the step it guards is never offered, and no error is raised anywhere. One
/// of the two files is TypeScript, so a type cannot hold them together — this does.
/// </summary>
public class ConditionFieldsAgreeTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the tests must be able to find the repository root");
        return dir!.FullName;
    }

    private static HashSet<string> EngineFields()
    {
        var path = Path.Combine(
            RepoRoot(), "backend", "src", "SimbaFlow.Infrastructure",
            "Workflow", "WorkflowEngineService.cs");
        var source = File.ReadAllText(path);

        var start = source.IndexOf("BuildCandidateFields(Candidate candidate)", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "BuildCandidateFields is what a condition is answered from");
        var end = source.IndexOf("\n    }", start, StringComparison.Ordinal);

        return Regex.Matches(source[start..end], @"\[""(?<key>[A-Za-z_]+)""\]")
            .Select(m => m.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ScreenFields()
    {
        var path = Path.Combine(RepoRoot(), "frontend", "lib", "workflow", "condition-fields.ts");
        var source = File.ReadAllText(path);

        var start = source.IndexOf("CANDIDATE_FIELDS", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        var end = source.IndexOf("\n];", start, StringComparison.Ordinal);

        return Regex.Matches(source[start..end], @"value:\s*""(?<key>[A-Za-z_]+)""")
            .Select(m => m.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void TheSetupScreenOffersExactlyWhatTheEngineCanAnswer()
    {
        var engine = EngineFields();
        var screen = ScreenFields();

        engine.Should().NotBeEmpty();
        screen.Should().NotBeEmpty();

        screen.Except(engine).Should().BeEmpty(
            "the screen would be offering a field the engine always reads as empty");
        engine.Except(screen).Should().BeEmpty(
            "the engine can answer a field nobody can choose on the screen");
    }
}
