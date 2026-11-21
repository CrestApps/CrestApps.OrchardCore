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
    [InlineData(ConditionOperator.NotContains, new[] { "All good" }, new[] { "refund" }, true)]
    [InlineData(ConditionOperator.NotContains, new[] { "I want a Refund" }, new[] { "refund" }, false)]
    [InlineData(ConditionOperator.StartsWith, new[] { "ACME Corp" }, new[] { "acme" }, true)]
    [InlineData(ConditionOperator.EndsWith, new[] { "admin@example.com" }, new[] { "EXAMPLE.COM" }, true)]
    [InlineData(ConditionOperator.EndsWith, new[] { "admin@example.com" }, new[] { "admin" }, false)]
    [InlineData(ConditionOperator.GreaterThanOrEqual, new[] { "9" }, new[] { "9" }, true)]
    [InlineData(ConditionOperator.GreaterThanOrEqual, new[] { "8" }, new[] { "9" }, false)]
    [InlineData(ConditionOperator.LessThanOrEqual, new[] { "6" }, new[] { "6" }, true)]
    [InlineData(ConditionOperator.LessThanOrEqual, new[] { "2026-05-02" }, new[] { "2026-05-01" }, false)]
    [InlineData(ConditionOperator.Between, new[] { "5" }, new[] { "1", "5" }, true)]
    [InlineData(ConditionOperator.Between, new[] { "0.5" }, new[] { "1", "5" }, false)]
    [InlineData(ConditionOperator.Between, new[] { "2026-01-15T10:00:00Z" }, new[] { "2026-01-01", "2026-01-31" }, true)]
    [InlineData(ConditionOperator.CountAtLeast, new[] { "a", "b" }, new[] { "2" }, true)]
    [InlineData(ConditionOperator.CountAtLeast, new[] { "a", " " }, new[] { "2" }, false)]
    [InlineData(ConditionOperator.CountAtMost, new[] { "a", "b", "c" }, new[] { "2" }, false)]
    [InlineData(ConditionOperator.CountAtMost, new string[0], new[] { "0" }, true)]
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

    [Fact]
    public void IsEmpty_IsFalseForARuleWithOnlyAGroup()
    {
        // Arrange
        var rule = new ConditionRule { Groups = [new ConditionRule { Conditions = [new Condition { Source = "q", Operator = ConditionOperator.IsNotEmpty }] }] };
        var emptyGroups = new ConditionRule { Groups = [new ConditionRule(), new ConditionRule { Conditions = [new Condition { Source = " " }] }] };

        // Act & Assert
        Assert.False(ConditionRule.IsEmpty(rule));
        Assert.True(ConditionRule.IsEmpty(emptyGroups));
    }

    [Theory]
    [InlineData(ConditionMatch.All, "Yes", "3", "", true)]
    [InlineData(ConditionMatch.All, "Yes", "9", "", false)]
    [InlineData(ConditionMatch.All, "Yes", "9", "very bad", true)]
    [InlineData(ConditionMatch.All, "No", "3", "", false)]
    [InlineData(ConditionMatch.Any, "No", "9", "", false)]
    [InlineData(ConditionMatch.Any, "No", "3", "", true)]
    public void Rule_MatchAppliesOverConditionsAndGroups(ConditionMatch match, string member, string score, string comment, bool met)
    {
        // Arrange: member is Yes AND (score <= 6 OR the comment says "bad").
        var rule = new ConditionRule
        {
            Match = match,
            Conditions = [new Condition { Source = "member", Operator = ConditionOperator.Is, Values = ["Yes"] }],
            Groups =
            [
                new ConditionRule
                {
                    Match = ConditionMatch.Any,
                    Conditions =
                    [
                        new Condition { Source = "score", Operator = ConditionOperator.LessThanOrEqual, Values = ["6"] },
                        new Condition { Source = "comment", Operator = ConditionOperator.Contains, Values = ["bad"] },
                    ],
                },
            ],
        };
        var answers = new Dictionary<string, string> { ["member"] = member, ["score"] = score, ["comment"] = comment };

        // Act
        var result = ConditionEvaluator.IsMet(rule, source => ConditionValue.Of(answers.GetValueOrDefault(source)));

        // Assert
        Assert.Equal(met, result);
    }

    [Fact]
    public void Rule_LeavesOutAGroupThatCannotBeJudged()
    {
        // Arrange: the group only reads a source the lookup no longer knows.
        var rule = new ConditionRule
        {
            Conditions = [new Condition { Source = "a", Operator = ConditionOperator.Is, Values = ["1"] }],
            Groups = [new ConditionRule { Conditions = [new Condition { Source = "removed", Operator = ConditionOperator.Is, Values = ["x"] }] }],
        };

        // Act
        var result = ConditionEvaluator.IsMet(rule, source => source == "a" ? ConditionValue.Of("1") : null);

        // Assert
        Assert.True(result);
    }
}
