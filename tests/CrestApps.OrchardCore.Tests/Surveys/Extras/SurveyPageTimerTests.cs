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

    [Fact]
    public void Deadline_PageSplitIntoScreens_CountsFromTheFirstScreenOfThePage()
    {
        // Arrange
        var entered = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var definition = new SurveyDefinition { Pages = [new SurveyPage { Id = "p1", TimeLimitSeconds = 60 }] };
        var second = "p1" + SurveyPlanBuilder.ScreenSeparator + "2";
        var response = new SurveyResponse
        {
            CurrentPageId = second,
            Plan = new SurveyResponsePlan
            {
                PageOrder = ["p1", second],
                ScreenPages = new Dictionary<string, string> { ["p1"] = "p1", [second] = "p1" },
            },
            PageVisits =
            [
                new SurveyPageVisit { PageId = "p1", EnteredUtc = entered, LeftUtc = entered.AddSeconds(50) },
                new SurveyPageVisit { PageId = second, EnteredUtc = entered.AddSeconds(50) },
            ],
        };

        // Act
        var deadline = SurveyPageTimer.Deadline(definition, response);
        var upAfter70 = SurveyPageTimer.PageTimeIsUp(definition, response, entered.AddSeconds(70));

        // Assert
        Assert.Equal(entered.AddSeconds(60), deadline);
        Assert.True(upAfter70);
    }

    [Fact]
    public async Task Next_PageSplitIntoScreens_GetsItsTimeLimitOnce()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"), RuntimeSurveys.Text("q2", required: true)),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q3")));

        definition.Pages[0].TimeLimitSeconds = 60;
        definition.Presentation = new SurveyPresentationSettings { QuestionsPerPage = 1 };

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act: the first screen takes 50 seconds, then the second screen is posted 20 seconds later without its
        // required answer. The page's 60 seconds ran out, so the respondent moves on.
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddSeconds(50);
        var first = await harness.PostAsync(response, identity, SurveyPageDirection.Next, ("q1", "a"));
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddSeconds(20);
        var second = await harness.PostAsync(first.Response, identity, SurveyPageDirection.Next);

        // Assert
        Assert.Equal(SurveyErrorCode.None, first.Error);
        Assert.NotEqual("p1", first.Response.CurrentPageId);
        Assert.Equal(SurveyErrorCode.None, second.Error);
        Assert.Equal("p2", second.Response.CurrentPageId);
    }
}
