using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The score a response gets when it completes: it counts the pages on the respondent's path, as a regrade and the review
/// do, and it is known before the ending is chosen, so an ending on the score reads this response's score.
/// </summary>
public sealed class SurveyCompletionScoringTests
{
    private static readonly SurveyRespondentIdentity _identity = new() { UserId = "u1" };

    [Fact]
    public async Task Completion_OfABranchedQuiz_LeavesTheSkippedTrackOut_AndARegradeAgrees()
    {
        // Arrange: track "a" (p2, two questions) ends the survey; track "b" (p3, two questions) is skipped. Pass at 60%.
        await using var harness = await CreateHarnessAsync();
        var definition = TrackQuiz();
        var survey = await harness.PublishAsync(definition);
        var response = (await harness.StartAsync(survey, "u1")).Response;

        var onTrackA = (await harness.PostAsync(response, _identity, SurveyPageDirection.Next, ("q1", "a"))).Response;

        // Act
        var done = await harness.PostAsync(onTrackA, _identity, SurveyPageDirection.Next, ("qa1", "right"), ("qa2", "right"));
        var stored = await harness.FindAsync(response.ItemId);

        var regraded = await harness.RunAsync(services =>
        {
            var copy = System.Text.Json.JsonSerializer.Deserialize<SurveyResponse>(System.Text.Json.JsonSerializer.Serialize(stored));
            services.GetRequiredService<ISurveyScoringEngine>().Score(definition, copy);

            return Task.FromResult(copy);
        });

        var hidden = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<SurveyLogicEngine>().Evaluate(definition, stored).HiddenQuestionIds));

        // Assert: 4 of 4, passed, right at completion; a regrade and the counted questions agree.
        Assert.True(done.Ended, done.Error + " " + string.Join(", ", done.Errors.SelectMany(pair => pair.Value)));
        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.Equal(4m, stored.Score);
        Assert.Equal(4m, stored.MaxScore);
        Assert.Equal(100m, stored.Percent);
        Assert.True(stored.Passed);
        Assert.Equal(stored.Score, regraded.Score);
        Assert.Equal(stored.MaxScore, regraded.MaxScore);
        Assert.Equal(stored.Passed, regraded.Passed);
        Assert.Equal(["q1", "qa1", "qa2"], SurveyScoringEngine.CountedQuestions(definition, stored, hidden).Select(question => question.Id));
    }

    [Fact]
    public async Task Completion_AtTheDeadline_OfABranchedQuiz_LeavesTheSkippedTrackOut()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var definition = TrackQuiz();
        definition.Timing.TimeLimitMinutes = 5;
        definition.Timing.GraceSeconds = 0;
        var survey = await harness.PublishAsync(definition);
        var response = (await harness.StartAsync(survey, "u1")).Response;

        var onTrackA = (await harness.PostAsync(response, _identity, SurveyPageDirection.Next, ("q1", "a"))).Response;
        await harness.ResponsesAsync(responses => responses.SaveAsync(new SavePageRequest
        {
            ResponseId = onTrackA.ItemId,
            Identity = _identity,
            PageId = onTrackA.CurrentPageId,
            Form = RuntimeTestHarness.Form(("qa1", "right"), ("qa2", "right")),
        }));

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(6);
        var submitted = await harness.ResponsesAsync(responses => responses.SubmitAtDeadlineAsync(response.ItemId));
        var stored = await harness.FindAsync(response.ItemId);

        // Assert
        Assert.True(submitted.Succeeded, submitted.Error.ToString());
        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.Equal(4m, stored.MaxScore);
        Assert.Equal(100m, stored.Percent);
    }

    [Fact]
    public async Task Ending_OnTheScore_ReadsThisCompletionsScore_AndAReopenClearsTheOldOne()
    {
        // Arrange: "passed" shows when the score is at least 4; "try" is the default ending.
        await using var harness = await CreateHarnessAsync();
        var definition = Quiz(RuntimeSurveys.Page("p1", Scored("q1"), Scored("q2")));
        definition.Navigation.AllowEditAfterSubmit = true;
        definition.Endings =
        [
            new SurveyEnding
            {
                Id = "passed",
                Name = "passed",
                Title = "Passed",
                Condition = new SurveyVisibility
                {
                    Mode = VisibilityMode.ShowWhen,
                    Rule = new ConditionRule { Conditions = [new Condition { Source = SurveyConditionSources.Score, Operator = ConditionOperator.GreaterThanOrEqual, Values = ["4"] }] },
                },
            },
            new SurveyEnding { Id = "try", Name = "try", Title = "Try again", IsDefault = true },
        ];

        var survey = await harness.PublishAsync(definition);
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act: all right, then reopened, then all wrong.
        await harness.PostAsync(response, _identity, SurveyPageDirection.Finish, ("q1", "right"), ("q2", "right"));
        var first = await harness.FindAsync(response.ItemId);

        var reopened = await harness.ResponsesAsync(responses => responses.ReopenAsync(response.ItemId, _identity));
        var open = await harness.FindAsync(response.ItemId);

        await harness.PostAsync(open, _identity, SurveyPageDirection.Finish, ("q1", "wrong"), ("q2", "wrong"));
        var second = await harness.FindAsync(response.ItemId);

        // Assert
        Assert.Equal(4m, first.Score);
        Assert.Equal("passed", first.EndingId);
        Assert.True(reopened.Succeeded, reopened.Error.ToString());
        Assert.Null(open.Score);
        Assert.Null(open.Percent);
        Assert.Null(open.Passed);
        Assert.Equal(0m, second.Score);
        Assert.Equal("try", second.EndingId);
    }

    private static Task<RuntimeTestHarness> CreateHarnessAsync()
        => RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddScoped<ISurveyScoringEngine, SurveyScoringEngine>();
            services.AddSurveyControlScorer<RightChoiceScorer>();
        });

    // p1 asks the track (no points): "a" goes on to p2, whose branch ends the survey; "b" jumps to p3.
    private static SurveyDefinition TrackQuiz()
    {
        var q1 = RuntimeSurveys.Choice("q1", "a", "b");
        q1.Scoring = new SurveyQuestionScoring { Points = 0 };

        var page1 = RuntimeSurveys.Page("p1", q1);
        page1.Branches.Add(new SurveyBranch
        {
            Id = "toB",
            Rule = new ConditionRule { Conditions = [new Condition { Source = "q1", Operator = ConditionOperator.Is, Values = ["b"] }] },
            TargetKind = BranchTargetKind.Page,
            TargetId = "p3",
        });

        var page2 = RuntimeSurveys.Page("p2", Scored("qa1"), Scored("qa2"));
        page2.Branches.Add(new SurveyBranch { Id = "done", TargetKind = BranchTargetKind.Ending, TargetId = "end" });

        var definition = Quiz(page1, page2, RuntimeSurveys.Page("p3", Scored("qb1"), Scored("qb2")));
        definition.Scoring.PassMarkPercent = 60;

        return definition;
    }

    private static SurveyDefinition Quiz(params SurveyPage[] pages)
    {
        var definition = RuntimeSurveys.Definition(pages);
        definition.Scoring.Enabled = true;

        return definition;
    }

    private static SurveyQuestion Scored(string id)
    {
        var question = RuntimeSurveys.Choice(id, "right", "wrong");
        question.Scoring = new SurveyQuestionScoring { Points = 2, CorrectChoiceIds = ["right"] };

        return question;
    }

    private sealed class RightChoiceScorer : ISurveyControlScorer
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
            => new() { Correct = IsRight(question, answer), CorrectChoiceIds = [.. question.Scoring?.CorrectChoiceIds ?? []] };

        private static bool IsRight(SurveyQuestion question, SurveyAnswer answer)
            => answer?.Values is { Count: > 0 } values && question.Scoring?.CorrectChoiceIds is { Count: > 0 } keys && values.Order().SequenceEqual(keys.Order());
    }
}
