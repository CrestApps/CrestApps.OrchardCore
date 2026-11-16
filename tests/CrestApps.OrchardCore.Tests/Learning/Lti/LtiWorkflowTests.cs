using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.Services;
using CrestApps.OrchardCore.Learning.Lti.Workflows;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Lti;

/// <summary>
/// The External Score Received event: which scores start it and what a workflow reads from its input.
/// </summary>
public sealed class LtiWorkflowTests
{
    [Fact]
    public void TheEvent_StartsForEveryScore_OrForTheListedGradingProgressOnly()
    {
        // Arrange
        var activity = new ExternalScoreReceivedEvent(new PassThroughStringLocalizer<ExternalScoreReceivedEvent>());

        // Act
        var anyScore = activity.CanExecute(Context(("GradingProgress", "Pending")), null);
        activity.GradingProgress = ["FullyGraded"];
        var graded = activity.CanExecute(Context(("GradingProgress", "FullyGraded")), null);
        var pending = activity.CanExecute(Context(("GradingProgress", "Pending")), null);
        var none = activity.CanExecute(Context(), null);

        // Assert
        Assert.True(anyScore);
        Assert.True(graded);
        Assert.False(pending);
        Assert.False(none);
    }

    [Fact]
    public void TheInput_CarriesTheToolTheLineItemAndTheScore_WithTheTimestampAsUtcRoundTrip()
    {
        // Arrange
        var context = new LtiScoreReceivedContext
        {
            Tool = new ExternalTool { ItemId = "tool1", Name = "Simulator", ClientId = "client1" },
            LineItem = new LtiLineItem { ItemId = "line1", CourseId = "course1", ResourceLinkId = "item1", Label = "Forklift run" },
            Score = new LtiScore
            {
                ItemId = "score1",
                UserId = "user1",
                ScoreGiven = 8,
                ScoreMaximum = 10,
                ActivityProgress = "Completed",
                GradingProgress = "FullyGraded",
                TimestampUtc = new DateTime(2026, 11, 16, 9, 30, 0, DateTimeKind.Unspecified),
            },
            Percent = 80,
            RecordedInProgress = true,
        };

        // Act
        var input = LtiScoreWorkflowObserver.Input(context);

        // Assert
        Assert.Equal("tool1", input["ToolId"]);
        Assert.Equal("Simulator", input["ToolName"]);
        Assert.Equal("client1", input["ClientId"]);
        Assert.Equal("course1", input["CourseId"]);
        Assert.Equal("item1", input["CourseItemId"]);
        Assert.Equal("line1", input["LineItemId"]);
        Assert.Equal("Forklift run", input["LineItemLabel"]);
        Assert.Equal("user1", input["UserId"]);
        Assert.Equal(8m, input["ScoreGiven"]);
        Assert.Equal(10m, input["ScoreMaximum"]);
        Assert.Equal(80m, input["Percent"]);
        Assert.Equal("Completed", input["ActivityProgress"]);
        Assert.Equal("FullyGraded", input["GradingProgress"]);
        Assert.Equal(true, input["RecordedInProgress"]);
        Assert.Equal("2026-11-16T09:30:00.0000000Z", input["TimestampUtc"]);
    }

    private static WorkflowExecutionContext Context(params (string Key, object Value)[] input)
        => new(
            new WorkflowType { WorkflowTypeId = "t1" },
            new Workflow { WorkflowId = "w1" },
            input.ToDictionary(pair => pair.Key, pair => pair.Value),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
