using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIAlertRuleTests
{
    private static readonly DateTime _now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_OpensAnIncidentOnce_KeepsItsWorstValue_AndResolvesItOnAMeasuredWindow()
    {
        // Arrange
        var rule = new AIAlertRule { Condition = AIConversationAlertCondition.Above, Threshold = 0.05, MinimumSampleSize = 10 };

        // Act
        var first = AIAlertRuleEvaluator.Apply(rule, new AIConversationMetricValue(0.08, 50), null, _now);
        var second = AIAlertRuleEvaluator.Apply(rule, new AIConversationMetricValue(0.12, 50), null, _now.AddMinutes(5));
        var tooFew = AIAlertRuleEvaluator.Apply(rule, new AIConversationMetricValue(0.01, 3), null, _now.AddMinutes(10));
        var back = AIAlertRuleEvaluator.Apply(rule, new AIConversationMetricValue(0.01, 40), null, _now.AddMinutes(15));

        // Assert
        Assert.Equal(AIAlertChange.Triggered, first);
        Assert.Equal(AIAlertChange.None, second);
        Assert.Equal(AIAlertChange.None, tooFew);
        Assert.Equal(AIAlertChange.Resolved, back);
        Assert.False(rule.IsOpen);

        var incident = Assert.Single(rule.Incidents);
        Assert.Equal(0.08, incident.OpenedValue);
        Assert.Equal(0.12, incident.PeakValue);
        Assert.Equal(_now.AddMinutes(15), incident.ResolvedUtc);
    }

    [Fact]
    public void Apply_ComparesWithTheWindowBefore_ForAChangeInPercent()
    {
        // Arrange
        var rule = new AIAlertRule { Condition = AIConversationAlertCondition.RisesByPercent, Threshold = 50, MinimumSampleSize = 0 };

        // Act
        var change = AIAlertRuleEvaluator.Apply(rule, new AIConversationMetricValue(30, 20), new AIConversationMetricValue(10, 20), _now);

        // Assert
        Assert.Equal(AIAlertChange.Triggered, change);
    }

    [Fact]
    public void Apply_KeepsOnlyTheLatestIncidents()
    {
        // Arrange
        var rule = new AIAlertRule { Condition = AIConversationAlertCondition.Above, Threshold = 1, MinimumSampleSize = 0 };

        // Act
        for (var i = 0; i < AIAlertRule.MaxIncidents + 5; i++)
        {
            AIAlertRuleEvaluator.Apply(rule, new AIConversationMetricValue(2, 1), null, _now.AddHours(i));
            AIAlertRuleEvaluator.Apply(rule, new AIConversationMetricValue(0, 1), null, _now.AddHours(i).AddMinutes(30));
        }

        // Assert
        Assert.Equal(AIAlertRule.MaxIncidents, rule.Incidents.Count);
        Assert.Equal(_now.AddHours(AIAlertRule.MaxIncidents + 4), rule.Incidents[0].OpenedUtc);
    }

    [Fact]
    public void IsDue_FollowsTheInterval()
    {
        // Arrange
        var rule = new AIAlertRule { CheckEveryMinutes = 5 };

        // Assert
        Assert.True(AIAlertRuleEvaluator.IsDue(rule, _now));

        rule.LastCheckedUtc = _now;
        Assert.False(AIAlertRuleEvaluator.IsDue(rule, _now.AddMinutes(4)));
        Assert.True(AIAlertRuleEvaluator.IsDue(rule, _now.AddMinutes(5)));

        rule.Enabled = false;
        Assert.False(AIAlertRuleEvaluator.IsDue(rule, _now.AddMinutes(10)));
    }
}
