using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Extras;

public sealed class SurveyPageTimerTests
{
    [Fact]
    public async Task Next_PageTimeRanOut_MovesOnWithoutTheMissingAnswer()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1", required: true)),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")));

        definition.Pages[0].TimeLimitSeconds = 60;

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var early = await harness.PostAsync(response, identity, SurveyPageDirection.Next);
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddSeconds(70);
        var late = await harness.PostAsync(early.Response, identity, SurveyPageDirection.Next);

        // Assert
        Assert.Equal(SurveyErrorCode.InvalidAnswer, early.Error);
        Assert.Equal(SurveyErrorCode.None, late.Error);
        Assert.Equal("p2", late.Response.CurrentPageId);
    }

    [Fact]
    public void Deadline_PageLimitBeforeResponseDeadline_IsThePageDeadline()
    {
        // Arrange
        var entered = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var definition = new SurveyDefinition { Pages = [new SurveyPage { Id = "p1", TimeLimitSeconds = 30 }] };
        var response = new SurveyResponse
        {
            CurrentPageId = "p1",
            DeadlineUtc = entered.AddMinutes(10),
            PageVisits = [new SurveyPageVisit { PageId = "p1", EnteredUtc = entered }],
        };

        // Act
        var deadline = SurveyPageTimer.Deadline(definition, response);

        // Assert
        Assert.Equal(entered.AddSeconds(30), deadline);
    }

    [Fact]
    public void Deadline_PageWithoutLimit_IsTheResponseDeadline()
    {
        // Arrange
        var deadline = new DateTime(2026, 11, 20, 10, 30, 0, DateTimeKind.Utc);
        var response = new SurveyResponse { CurrentPageId = "p1", DeadlineUtc = deadline };

        // Act
        var result = SurveyPageTimer.Deadline(new SurveyDefinition { Pages = [new SurveyPage { Id = "p1" }] }, response);

        // Assert
        Assert.Equal(deadline, result);
    }
}
