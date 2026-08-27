using System.Text.Json;
using FluentAssertions;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Tests.Services;

public class ConditionEvaluatorTests
{
    [Fact]
    public void Evaluate_EmptyConditions_ReturnsTrue()
    {
        var conditions = JsonDocument.Parse("{}");
        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>())
            .Should().BeTrue();
    }

    [Fact]
    public void Evaluate_EmptyRulesArray_ReturnsTrue()
    {
        var conditions = JsonDocument.Parse("""{"operator":"AND","rules":[]}""");
        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>())
            .Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Eq_Match_ReturnsTrue()
    {
        var conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"medical","op":"eq","value":"Fit"}]}""");

        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>
        {
            ["medical"] = "Fit"
        }).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Eq_Mismatch_ReturnsFalse()
    {
        var conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"medical","op":"eq","value":"Fit"}]}""");

        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>
        {
            ["medical"] = "Unfit"
        }).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_And_RequiresAllRules()
    {
        var conditions = JsonDocument.Parse("""
            {
              "operator": "AND",
              "rules": [
                { "field": "medical", "op": "eq", "value": "Fit" },
                { "field": "tasheer", "op": "eq", "value": "Book Done" }
              ]
            }
            """);

        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>
        {
            ["medical"] = "Fit",
            ["tasheer"] = "Booked"
        }).Should().BeFalse();

        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>
        {
            ["medical"] = "Fit",
            ["tasheer"] = "Book Done"
        }).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Or_RequiresAnyRule()
    {
        var conditions = JsonDocument.Parse("""
            {
              "operator": "OR",
              "rules": [
                { "field": "visa", "op": "eq", "value": "Issued" },
                { "field": "visa", "op": "eq", "value": "Submitted" }
              ]
            }
            """);

        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>
        {
            ["visa"] = "Submitted"
        }).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NotEmpty_And_Empty()
    {
        var notEmpty = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"labourId","op":"not_empty"}]}""");
        var empty = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"labourId","op":"empty"}]}""");

        var fields = new Dictionary<string, string?> { ["labourId"] = "L-1" };
        ConditionEvaluator.Evaluate(notEmpty, new Dictionary<string, string>(), fields).Should().BeTrue();
        ConditionEvaluator.Evaluate(empty, new Dictionary<string, string>(), fields).Should().BeFalse();

        fields["labourId"] = null;
        ConditionEvaluator.Evaluate(notEmpty, new Dictionary<string, string>(), fields).Should().BeFalse();
        ConditionEvaluator.Evaluate(empty, new Dictionary<string, string>(), fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_In_Operator()
    {
        var conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"status","op":"in","value":["Ready","Issued"]}]}""");

        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>
        {
            ["status"] = "Ready"
        }).Should().BeTrue();

        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>
        {
            ["status"] = "Pending"
        }).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_Neq_Operator()
    {
        var conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"visa","op":"neq","value":"Rejected"}]}""");

        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>
        {
            ["visa"] = "Issued"
        }).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_FallsBackToCandidateFields()
    {
        var conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"passportNumber","op":"eq","value":"AB123"}]}""");

        ConditionEvaluator.Evaluate(
            conditions,
            new Dictionary<string, string>(),
            new Dictionary<string, string?> { ["passportNumber"] = "AB123" })
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("fit")]
    [InlineData("FIT")]
    [InlineData(" Fit ")]
    public void Evaluate_Eq_IgnoresCaseAndSurroundingSpace(string stored)
    {
        var conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"medical","op":"eq","value":"Fit"}]}""");

        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>
        {
            ["medical"] = stored
        }).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_In_IgnoresCase()
    {
        var conditions = JsonDocument.Parse(
            """{"operator":"OR","rules":[{"field":"visa","op":"in","value":["Ready","Submitted"]}]}""");

        ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string>
        {
            ["visa"] = "submitted"
        }).Should().BeTrue();
    }

    [Fact]
    public void TryEvaluate_Met_HasNoReason()
    {
        var conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"visa","op":"eq","value":"Issued"}]}""");

        var met = ConditionEvaluator.TryEvaluate(
            conditions,
            new Dictionary<string, string> { ["visa"] = "Issued" },
            null,
            out var reason);

        met.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void TryEvaluate_Blocked_NamesFieldExpectedAndActual()
    {
        var conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"visa","op":"eq","value":"Issued"}]}""");

        var met = ConditionEvaluator.TryEvaluate(
            conditions,
            new Dictionary<string, string> { ["visa"] = "Ready" },
            null,
            out var reason);

        met.Should().BeFalse();
        reason.Should().Be("Visa must be Issued (currently Ready)");
    }

    [Fact]
    public void TryEvaluate_Blocked_MissingValueReadsAsNotSet()
    {
        var conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"tasheer","op":"eq","value":"Book Done"}]}""");

        ConditionEvaluator.TryEvaluate(
            conditions, new Dictionary<string, string>(), null, out var reason)
            .Should().BeFalse();

        reason.Should().Be("Tasheer must be Book Done (currently not set)");
    }

    [Fact]
    public void TryEvaluate_AndGroup_ReportsOnlyTheFailingRule()
    {
        var conditions = JsonDocument.Parse(
            """
            {"operator":"AND","rules":[
              {"field":"medical","op":"eq","value":"Fit"},
              {"field":"tasheer","op":"eq","value":"Book Done"}
            ]}
            """);

        ConditionEvaluator.TryEvaluate(
            conditions,
            new Dictionary<string, string> { ["medical"] = "Fit" },
            null,
            out var reason).Should().BeFalse();

        reason.Should().Be("Tasheer must be Book Done (currently not set)");
    }

    [Fact]
    public void TryEvaluate_UnderscoredField_ReadsAsWords()
    {
        var conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"flight_date","op":"not_empty"}]}""");

        ConditionEvaluator.TryEvaluate(
            conditions, new Dictionary<string, string>(), null, out var reason)
            .Should().BeFalse();

        reason.Should().Be("Flight date is required");
    }
}
