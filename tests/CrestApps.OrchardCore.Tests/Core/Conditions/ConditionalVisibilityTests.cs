using CrestApps.OrchardCore.Conditions;

namespace CrestApps.OrchardCore.Tests.Core.Conditions;

/// <summary>
/// Which questions of a set show when their rules read each other (<see cref="ConditionalVisibility"/>).
/// </summary>
public sealed class ConditionalVisibilityTests
{
    private static ConditionRule When(string source, string value)
        => new() { Conditions = [new Condition { Source = source, Operator = ConditionOperator.Is, Values = [value] }] };

    private static ConditionRule WhenEmpty(string source)
        => new() { Conditions = [new Condition { Source = source, Operator = ConditionOperator.IsEmpty }] };

    [Fact]
    public void Chain_ClosesWhenAnEarlyAnswerChanges()
    {
        // Arrange: outage -> affected (when outage is Yes) -> count (when affected is Many).
        var rules = new Dictionary<string, ConditionRule>
        {
            ["affected"] = When("outage", "Yes"),
            ["count"] = When("affected", "Many"),
        };

        // The stale answer "Many" is still stored for "affected", but "affected" no longer shows.
        var answers = new Dictionary<string, string> { ["outage"] = "No", ["affected"] = "Many", ["count"] = "40" };

        // Act
        var hidden = ConditionalVisibility.GetHidden(rules, source => answers.TryGetValue(source, out var value) ? ConditionValue.Of(value) : null);

        // Assert
        Assert.Equal(["affected", "count"], hidden.Order());
    }

    [Fact]
    public void HiddenSource_FailsEvenAnIsEmptyCondition()
    {
        // Arrange: "details" shows when "reason" is empty, but "reason" itself is hidden.
        var rules = new Dictionary<string, ConditionRule>
        {
            ["reason"] = When("outage", "Yes"),
            ["details"] = WhenEmpty("reason"),
        };
        var answers = new Dictionary<string, string> { ["outage"] = "No" };

        // Act
        var hidden = ConditionalVisibility.GetHidden(rules, source => source is "outage" or "reason" or "details" ? ConditionValue.Of(answers.GetValueOrDefault(source)) : null);

        // Assert
        Assert.Contains("details", hidden);
    }

    [Fact]
    public void Cycle_ShowsTheItemsAndIsFound()
    {
        // Arrange: a shows when b, b shows when a.
        var rules = new Dictionary<string, ConditionRule>
        {
            ["a"] = When("b", "x"),
            ["b"] = When("a", "x"),
            ["c"] = When("d", "x"),
        };

        // Act
        var hidden = ConditionalVisibility.GetHidden(rules, _ => ConditionValue.Empty);
        var cycle = ConditionalVisibility.FindCycle(rules);

        // Assert: a broken definition never hides a question, and an editor can name the cycle.
        Assert.DoesNotContain("a", hidden);
        Assert.DoesNotContain("b", hidden);
        Assert.Contains("c", hidden);
        Assert.Equal(["a", "b"], cycle.Order());
        Assert.Empty(ConditionalVisibility.FindCycle(new Dictionary<string, ConditionRule> { ["c"] = When("d", "x") }));
    }

    [Fact]
    public void Sources_WalkNestedGroups()
    {
        // Arrange
        var rule = new ConditionRule
        {
            Conditions = [new Condition { Source = "a", Operator = ConditionOperator.IsNotEmpty }],
            Groups =
            [
                new ConditionRule
                {
                    Conditions = [new Condition { Source = "b", Operator = ConditionOperator.IsNotEmpty }],
                    Groups = [new ConditionRule { Conditions = [new Condition { Source = "c", Operator = ConditionOperator.IsNotEmpty }, new Condition { Source = "a", Operator = ConditionOperator.IsEmpty }] }],
                },
            ],
        };

        // Act
        var sources = ConditionalVisibility.Sources(rule).ToArray();

        // Assert
        Assert.Equal(["a", "b", "c"], sources);
    }

    [Fact]
    public void FindCycle_FollowsSourcesInNestedGroups()
    {
        // Arrange: a reads b only through a nested group.
        var rules = new Dictionary<string, ConditionRule>
        {
            ["a"] = new ConditionRule { Groups = [When("b", "x")] },
            ["b"] = When("a", "x"),
        };

        // Act
        var cycle = ConditionalVisibility.FindCycle(rules);
        var hidden = ConditionalVisibility.GetHidden(rules, _ => ConditionValue.Empty);

        // Assert
        Assert.Equal(["a", "b"], cycle.Order());
        Assert.Empty(hidden);
    }
}
