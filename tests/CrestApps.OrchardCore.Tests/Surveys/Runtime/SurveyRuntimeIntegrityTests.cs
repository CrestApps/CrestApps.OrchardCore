using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// Time limits, locked answers and reopening can't be used to change a response or a count after the fact.
/// </summary>
public sealed class SurveyRuntimeIntegrityTests
{
    [Fact]
    public async Task Save_AfterTheDeadlineAndGrace_IsRefusedAndSubmitsWhatWasSavedInTime()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")), RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")));
        definition.Timing.TimeLimitMinutes = 1;
        definition.Timing.GraceSeconds = 0;
        definition.Timing.AutoSubmitAtDeadline = false;

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;
        var early = await SaveAsync(harness, response, identity, ("q1", "early"));

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(75);
        var late = await SaveAsync(harness, response, identity, ("q1", "late"));

        // Assert
        Assert.True(early.Succeeded);
        Assert.Equal(SurveyErrorCode.DeadlinePassed, late.Error);

        var stored = await harness.FindAsync(response.ItemId);

        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.Equal("early", stored.Answers["q1"].Values[0]);
    }

    [Fact]
    public async Task Check_AfterTheDeadlineAndGrace_IsRefusedAndLocksNothing()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Choice("q1", "a", "b")), RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")));
        definition.Timing.TimeLimitMinutes = 1;
        definition.Timing.GraceSeconds = 0;
        definition.Scoring.Enabled = true;
        definition.Scoring.FeedbackMode = FeedbackMode.Immediate;

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(75);
        var late = await CheckAsync(harness, response, identity, "q1", ("q1", "a"));

        // Assert
        Assert.Equal(SurveyErrorCode.DeadlinePassed, late.Error);

        var stored = await harness.FindAsync(response.ItemId);

        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.False(stored.Answers.ContainsKey("q1"));
    }

    [Fact]
    public async Task SubmitAtDeadline_DropsSavedAnswersThatBreakTheirRules()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Number("q1"), RuntimeSurveys.Text("q2")));
        definition.Timing.TimeLimitMinutes = 1;

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Autosave keeps answers without validating them.
        await SaveAsync(harness, response, identity, ("q1", "not a number"), ("q2", "kept"));

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(2);
        var result = await harness.ResponsesAsync(responses => responses.SubmitAtDeadlineAsync(response.ItemId));

        // Assert
        Assert.True(result.Succeeded);

        var stored = await harness.FindAsync(response.ItemId);

        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.False(stored.Answers.ContainsKey("q1"));
        Assert.Equal("kept", stored.Answers["q2"].Values[0]);
    }

    [Fact]
    public async Task PageTimer_IsMeasuredFromTheFirstVisit_AndLateValuesAreIgnored()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var page1 = RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"));
        page1.TimeLimitSeconds = 60;

        var definition = RuntimeSurveys.Definition(page1, RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")), RuntimeSurveys.Page("p3", RuntimeSurveys.Text("q3")));
        definition.Timing.GraceSeconds = 0;

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(10);
        var onPage2 = (await harness.PostAsync(response, identity, SurveyPageDirection.Next, ("q1", "in time"))).Response;

        // Going back adds a visit; it must not give the page a new minute.
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(20);
        var backOnPage1 = (await harness.PostAsync(onPage2, identity, SurveyPageDirection.Back)).Response;

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(65);
        var save = await SaveAsync(harness, backOnPage1, identity, ("q1", "late save"));
        var next = await harness.PostAsync(backOnPage1, identity, SurveyPageDirection.Next, ("q1", "late post"));

        // Assert
        Assert.Equal("p1", backOnPage1.CurrentPageId);
        Assert.Equal(RuntimeTestHarness.Now.AddSeconds(60), SurveyPageTimer.Deadline(definition, backOnPage1));
        Assert.Equal(SurveyErrorCode.InvalidState, save.Error);
        Assert.True(next.Succeeded);
        Assert.Equal("p2", next.Response.CurrentPageId);
        Assert.Equal("in time", (await harness.FindAsync(response.ItemId)).Answers["q1"].Values[0]);
    }

    [Fact]
    public async Task Back_IntoATimedPageWhoseTimeIsUp_IsRefused()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var page1 = RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"));
        page1.TimeLimitSeconds = 60;

        var definition = RuntimeSurveys.Definition(page1, RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")));
        definition.Timing.GraceSeconds = 0;

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(10);
        var onPage2 = (await harness.PostAsync(response, identity, SurveyPageDirection.Next, ("q1", "a"))).Response;

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(90);
        var back = await harness.PostAsync(onPage2, identity, SurveyPageDirection.Back);

        // Assert
        Assert.Equal("p2", back.Response.CurrentPageId);
        Assert.False(back.Page.CanGoBack);
    }

    [Fact]
    public async Task LockedAnswer_SurvivesHidingItsQuestion_AndCanNotBeAnsweredAgain()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var q1 = RuntimeSurveys.Choice("q1", "yes", "no");
        var q2 = RuntimeSurveys.Choice("q2", "a", "b");
        q2.Visibility = RuntimeSurveys.ShowWhen("q1", ConditionOperator.Is, "yes");

        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1, q2), RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q3")));
        definition.Scoring.Enabled = true;
        definition.Scoring.FeedbackMode = FeedbackMode.Immediate;

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await SaveAsync(harness, response, identity, ("q1", "yes"));
        var check = await CheckAsync(harness, response, identity, "q2", ("q1", "yes"), ("q2", "b"));

        // Act
        await SaveAsync(harness, response, identity, ("q1", "no"));
        var hidden = await harness.FindAsync(response.ItemId);
        await SaveAsync(harness, response, identity, ("q1", "yes"), ("q2", "a"));
        var again = await CheckAsync(harness, response, identity, "q2", ("q1", "yes"), ("q2", "a"));

        // Assert
        Assert.True(check.Succeeded);
        Assert.Equal("b", hidden.Answers["q2"].Values[0]);
        Assert.NotNull(hidden.Answers["q2"].CheckedUtc);
        Assert.Equal(SurveyErrorCode.InvalidState, again.Error);
        Assert.Equal("b", (await harness.FindAsync(response.ItemId)).Answers["q2"].Values[0]);
    }

    [Fact]
    public async Task LockedAnswer_KeepsAChoiceThatIsHiddenLater()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var q1 = RuntimeSurveys.Choice("q1", "yes", "no");
        var q2 = RuntimeSurveys.Choice("q2", "a", "b");
        q2.Choices[1].Visibility = RuntimeSurveys.HideWhen("q1", ConditionOperator.Is, "no");

        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1, q2), RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q3")));
        definition.Scoring.Enabled = true;
        definition.Scoring.FeedbackMode = FeedbackMode.Immediate;

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await CheckAsync(harness, response, identity, "q2", ("q1", "yes"), ("q2", "a"), ("q2", "b"));

        // Act
        await SaveAsync(harness, response, identity, ("q1", "no"));

        // Assert
        Assert.Equal(["a", "b"], (await harness.FindAsync(response.ItemId)).Answers["q2"].Values);
    }

    [Fact]
    public async Task Check_WithImmediateFeedback_ShowsTheFeedbackTheReviewAllows()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddSingleton<ISurveyReviewPolicy, SurveyReviewPolicy>();
            services.AddSurveyControlScorer<KeyedChoiceScorer>();
        });

        var q1 = RuntimeSurveys.Choice("q1", "a", "b");
        q1.Scoring = new SurveyQuestionScoring { Points = 2, CorrectChoiceIds = ["a"] };

        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1), RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")));
        definition.Scoring.Enabled = true;
        definition.Scoring.FeedbackMode = FeedbackMode.Immediate;
        definition.Review.During = ReviewShows.Score | ReviewShows.RightAnswers;

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var check = await CheckAsync(harness, response, identity, "q1", ("q1", "b"));

        // Assert
        Assert.True(check.Succeeded);
        Assert.Equal((decimal?)0, check.Points);
        Assert.False(check.Feedback.Correct);
        Assert.Equal(["a"], check.Feedback.CorrectChoiceIds);
    }

    [Fact]
    public async Task Reopen_ThenCompleteAgain_CountsParticipationAndQuotasOnce()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSurveysCollectingExtras());
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Choice("q1", "a", "b")));
        definition.Navigation.AllowEditAfterSubmit = true;
        definition.Quotas.Add(new SurveyQuota { Id = "qa", Name = "qa", Limit = 5, Action = QuotaAction.None });

        var survey = await harness.PublishAsync(definition);
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await harness.PostAsync(response, identity, SurveyPageDirection.Finish, ("q1", "a"));

        // Act
        var reopened = await harness.ResponsesAsync(responses => responses.ReopenAsync(response.ItemId, identity));
        var again = await harness.PostAsync(reopened.Response, identity, SurveyPageDirection.Finish, ("q1", "b"));

        // Assert
        Assert.True(reopened.Succeeded);
        Assert.True(again.Ended);

        var quotas = await harness.RunAsync(services => services.GetRequiredService<ISurveyQuotaService>().GetCountsAsync(survey.ItemId));
        var counts = await harness.RunAsync(services => services.GetRequiredService<SurveyParticipationService>().CountAsync(survey, "u:u1", null, harness.Clock.UtcNow));

        Assert.Equal(1, quotas["qa"]);
        Assert.Equal(1, counts.Completed.Total);
        Assert.Equal(1, counts.Started.Total);
    }

    [Fact]
    public async Task Maintenance_AResponseThatFails_IsLoggedAndSkipped_AndTheNextOneIsStillSubmitted()
    {
        // Arrange
        var engine = new PoisonScoringEngine();
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSingleton<ISurveyScoringEngine>(engine));
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        definition.Timing.TimeLimitMinutes = 1;
        definition.Scoring.Enabled = true;

        var survey = await harness.PublishAsync(definition);
        var poisoned = (await harness.StartAsync(survey, "u1")).Response;

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(30);
        var healthy = (await harness.StartAsync(survey, "u2")).Response;

        engine.PoisonedResponseId = poisoned.ItemId;

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(10);
        await harness.RunInShellAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        Assert.Equal(SurveyResponseStatus.InProgress, (await harness.FindAsync(poisoned.ItemId)).Status);
        Assert.Equal(SurveyResponseStatus.Completed, (await harness.FindAsync(healthy.ItemId)).Status);
    }

    private static Task<SurveySaveResult> SaveAsync(RuntimeTestHarness harness, SurveyResponse response, SurveyRespondentIdentity identity, params (string Key, string Value)[] fields)
        => harness.ResponsesAsync(responses => responses.SaveAsync(new SavePageRequest
        {
            ResponseId = response.ItemId,
            Identity = identity,
            PageId = response.CurrentPageId,
            Form = RuntimeTestHarness.Form(fields),
        }));

    private static Task<SurveyCheckResult> CheckAsync(RuntimeTestHarness harness, SurveyResponse response, SurveyRespondentIdentity identity, string questionId, params (string Key, string Value)[] fields)
        => harness.ResponsesAsync(responses => responses.CheckAnswerAsync(new CheckAnswerRequest
        {
            ResponseId = response.ItemId,
            Identity = identity,
            QuestionId = questionId,
            Form = RuntimeTestHarness.Form(fields),
        }));

    private sealed class KeyedChoiceScorer : ISurveyControlScorer
    {
        public string ControlType => RuntimeChoiceControl.Name;

        public SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context)
        {
            var max = MaxPoints(question, context);

            return IsRight(question, answer) ? SurveyScoreResult.Of(max, max) : new SurveyScoreResult { Points = 0, MaxPoints = max, Correct = false };
        }

        public decimal MaxPoints(SurveyQuestion question, SurveyScoringContext context)
            => context.PointsOf(question);

        public SurveyAnswerFeedback GetFeedback(SurveyQuestion question, SurveyAnswer answer)
            => new() { Correct = IsRight(question, answer), CorrectChoiceIds = [.. question.Scoring.CorrectChoiceIds] };

        private static bool IsRight(SurveyQuestion question, SurveyAnswer answer)
            => answer?.Values is { Count: > 0 } values && values.Order().SequenceEqual(question.Scoring.CorrectChoiceIds.Order());
    }

    private sealed class PoisonScoringEngine : ISurveyScoringEngine
    {
        public string PoisonedResponseId { get; set; }

        public void Score(SurveyDefinition definition, SurveyResponse response)
        {
            if (response.ItemId == PoisonedResponseId)
            {
                throw new OverflowException("Value was either too large or too small for a Decimal.");
            }

            response.Grading = GradingStatus.Graded;
        }
    }
}
