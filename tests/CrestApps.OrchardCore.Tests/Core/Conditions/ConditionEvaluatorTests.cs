using CrestApps.OrchardCore.Conditions;

namespace CrestApps.OrchardCore.Tests.Core.Conditions;

/// <summary>
/// The shared rules that say when a question, a field or a step shows (<see cref="ConditionEvaluator"/>).
/// </summary>
public sealed class ConditionEvaluatorTests
{
    [Theory]
    [InlineData(ConditionOperator.Is, new[] { "Yes" }, new[] { "yes" }, true)]
    [InlineData(ConditionOperator.Is, new[] { "No" }, new[] { "Yes" }, false)]
    [InlineData(ConditionOperator.Is, new[] { "a", "b" }, new[] { "b" }, true)]
    [InlineData(ConditionOperator.IsNot, new[] { "No" }, new[] { "Yes" }, true)]
    [InlineData(ConditionOperator.IsNot, new string[0], new[] { "Yes" }, true)]
    [InlineData(ConditionOperator.IsOneOf, new[] { "Printer" }, new[] { "Laptop", "Printer" }, true)]
    [InlineData(ConditionOperator.IsOneOf, new[] { "Phone" }, new[] { "Laptop", "Printer" }, false)]
    [InlineData(ConditionOperator.IsNoneOf, new[] { "Phone" }, new[] { "Laptop", "Printer" }, true)]
    [InlineData(ConditionOperator.IsEmpty, new[] { " " }, new string[0], true)]
    [InlineData(ConditionOperator.IsEmpty, new[] { "x" }, new string[0], false)]
    [InlineData(ConditionOperator.IsNotEmpty, new[] { "x" }, new string[0], true)]
    [InlineData(ConditionOperator.IsNotEmpty, new string[0], new string[0], false)]
    [InlineData(ConditionOperator.Contains, new[] { "The screen is black" }, new[] { "SCREEN" }, true)]
    [InlineData(ConditionOperator.GreaterThan, new[] { "12.5" }, new[] { "10" }, true)]
    [InlineData(ConditionOperator.GreaterThan, new[] { "9" }, new[] { "10" }, false)]
    [InlineData(ConditionOperator.GreaterThan, new[] { "many" }, new[] { "10" }, false)]
    [InlineData(ConditionOperator.LessThan, new[] { "9" }, new[] { "10" }, true)]
    public void Operators_CompareTheSourceWithTheValues(ConditionOperator op, string[] actual, string[] expected, bool met)
    {
        // Arrange
        var condition = new Condition { Source = "q", Operator = op, Values = [.. expected] };

        // Act
        var result = ConditionEvaluator.IsMet(condition, new ConditionValue(actual));

        // Assert
        Assert.Equal(met, result);
    }

    [Fact]
    public void IsOrUnder_MatchesTheValueAndWhatItSitsUnder()
    {
        // Arrange: Laptop sits under Hardware.
        var laptop = new ConditionValue(["laptop"], ["hardware"]);
        var underHardware = new Condition { Source = "category", Operator = ConditionOperator.IsOrUnder, Values = ["hardware"] };
        var underSoftware = new Condition { Source = "category", Operator = ConditionOperator.IsOrUnder, Values = ["software"] };
        var isHardware = new Condition { Source = "category", Operator = ConditionOperator.Is, Values = ["hardware"] };

        // Act & Assert
        Assert.True(ConditionEvaluator.IsMet(underHardware, laptop));
        Assert.False(ConditionEvaluator.IsMet(underSoftware, laptop));
        Assert.False(ConditionEvaluator.IsMet(isHardware, laptop));
    }

    [Fact]
    public void WithheldSource_FailsEveryOperator()
    {
        foreach (var op in Enum.GetValues<ConditionOperator>())
        {
            Assert.False(ConditionEvaluator.IsMet(new Condition { Source = "q", Operator = op, Values = ["x"] }, ConditionValue.Withheld));
        }
    }

    [Theory]
    [InlineData(ConditionMatch.All, "Yes", "5", true)]
    [InlineData(ConditionMatch.All, "Yes", "1", false)]
    [InlineData(ConditionMatch.Any, "No", "5", true)]
    [InlineData(ConditionMatch.Any, "No", "1", false)]
    public void Rule_NeedsAllOrAnyCondition(ConditionMatch match, string outage, string users, bool met)
    {
        // Arrange
        var rule = new ConditionRule
        {
            Match = match,
            Conditions =
            [
                new Condition { Source = "outage", Operator = ConditionOperator.Is, Values = ["Yes"] },
                new Condition { Source = "users", Operator = ConditionOperator.GreaterThan, Values = ["3"] },
            ],
        };
        var answers = new Dictionary<string, string> { ["outage"] = outage, ["users"] = users };

        // Act
        var result = ConditionEvaluator.IsMet(rule, source => ConditionValue.Of(answers.GetValueOrDefault(source)));

        // Assert
        Assert.Equal(met, result);
    }

    [Fact]
    public void Rule_LeavesOutConditionsOnSourcesNoLongerKnown()
    {
        // Arrange: "removed" was a field that was deleted; the lookup doesn't know it.
        var rule = new ConditionRule
        {
            Conditions =
            [
                new Condition { Source = "removed", Operator = ConditionOperator.Is, Values = ["x"] },
                new Condition { Source = "outage", Operator = ConditionOperator.Is, Values = ["Yes"] },
            ],
        };
        var onlyRemoved = new ConditionRule { Conditions = [rule.Conditions[0]] };

        ConditionValue Lookup(string source) => source == "outage" ? ConditionValue.Of("Yes") : null;

        // Act & Assert: the known condition decides, and a rule with nothing left applies.
        Assert.True(ConditionEvaluator.IsMet(rule, Lookup));
        Assert.True(ConditionEvaluator.IsMet(onlyRemoved, Lookup));
    }

    [Fact]
    public void EmptyRule_AlwaysApplies()
    {
        Assert.True(ConditionEvaluator.IsMet(null, _ => null));
        Assert.True(ConditionEvaluator.IsMet(new ConditionRule(), _ => null));
        Assert.True(ConditionRule.IsEmpty(new ConditionRule { Conditions = [new Condition { Source = " " }] }));
    }
}
