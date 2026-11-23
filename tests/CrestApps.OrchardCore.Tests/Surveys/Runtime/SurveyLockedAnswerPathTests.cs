using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Api;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// With immediate feedback, a respondent can't take an answer they already checked (and locked) off their path or hide
/// it by changing another answer afterwards: every way of saving answers refuses such a change and saves nothing. The
/// author's own logic (a rule on the time) may still hide a locked answer, and it keeps counting.
/// </summary>
public sealed class SurveyLockedAnswerPathTests
{
    private const string LockMessage = "You can't change this answer now: the change would skip or hide a question whose answer you already checked. Keep your earlier answer to go on.";

    private static readonly SurveyRespondentIdentity _identity = new() { UserId = "u1" };

    [Fact]
    public async Task Next_AfterBack_ThatTakesABranchSkippingALockedAnswer_IsRefused_AndTheScoreIsUnchanged()
    {
        // Arrange: q1 "b" goes to branch B (p3), "a" to branch A (p2); both branches go on to p4.
        await using var harness = await CreateHarnessAsync();
        var survey = await harness.PublishAsync(BranchQuiz());
        var response = (await harness.StartAsync(survey, "u1")).Response;

        var onBranchB = (await harness.PostAsync(response, _identity, SurveyPageDirection.Next, ("q1", "b"))).Response;
        var check = await CheckAsync(harness, onBranchB, "qb", ("qb", "right"));
        var backOnP1 = (await harness.PostAsync(onBranchB, _identity, SurveyPageDirection.Back)).Response;

        // Act: the respondent changes the branching answer to leave branch B.
        var refused = await harness.PostAsync(backOnP1, _identity, SurveyPageDirection.Next, ("q1", "a"));
        var stored = await harness.FindAsync(response.ItemId);

        // Assert: nothing is saved, the error is on the branching answer, and the respondent is still on p1.
        Assert.True(check.Succeeded, string.Join(", ", check.Errors));
        Assert.Equal("p1", backOnP1.CurrentPageId);
        Assert.Equal(SurveyErrorCode.InvalidAnswer, refused.Error);
        Assert.Equal([LockMessage], refused.Errors["q1"]);
        Assert.Equal("p1", refused.Page.Id);
        Assert.Equal("p1", stored.CurrentPageId);
        Assert.Equal("b", stored.Answers["q1"].Values[0]);
        Assert.True(stored.Answers["qb"].IsLocked);

        // The respondent keeps branch B and finishes: the score counts branch B only.
        var again = await harness.PostAsync(stored, _identity, SurveyPageDirection.Next, ("q1", "b"));
        var finished = await harness.PostAsync(again.Response, _identity, SurveyPageDirection.Next);
        var done = await harness.PostAsync(finished.Response, _identity, SurveyPageDirection.Finish, ("q4", "right"));
        var completed = await harness.FindAsync(response.ItemId);

        Assert.True(again.Succeeded);
        Assert.Equal("p3", again.Response.CurrentPageId);
        Assert.True(done.Ended, done.Error + " " + string.Join(", ", done.Errors.SelectMany(pair => pair.Value)));
        Assert.Equal(SurveyResponseStatus.Completed, completed.Status);
        Assert.Equal(["p1", "p3", "p4"], completed.PagePath);
        Assert.Equal(2m, completed.Score);
        Assert.Equal(2m, completed.MaxScore);
    }

    [Fact]
    public async Task Next_ThatBranchesToAnEndingBeforeALockedAnswer_IsRefused()
    {
        // Arrange: on p1, "stop" ends the survey at once; the respondent locked an answer on p2.
        await using var harness = await CreateHarnessAsync();
        var page1 = RuntimeSurveys.Page("p1", RuntimeSurveys.Choice("q1", "go", "stop"));
        page1.Branches.Add(new SurveyBranch { Id = "b1", Rule = Is("q1", "stop"), TargetKind = BranchTargetKind.Ending, TargetId = "end" });
        var definition = Immediate(RuntimeSurveys.Definition(page1, RuntimeSurveys.Page("p2", Scored("q2")), RuntimeSurveys.Page("p3", RuntimeSurveys.Text("q3"))));
        var survey = await harness.PublishAsync(definition);
        var response = (await harness.StartAsync(survey, "u1")).Response;

        var onP2 = (await harness.PostAsync(response, _identity, SurveyPageDirection.Next, ("q1", "go"))).Response;
        await CheckAsync(harness, onP2, "q2", ("q2", "right"));
        var backOnP1 = (await harness.PostAsync(onP2, _identity, SurveyPageDirection.Back)).Response;

        // Act: finishing from p1 with "stop" would end before the locked answer's page.
        var refused = await harness.PostAsync(backOnP1, _identity, SurveyPageDirection.Finish, ("q1", "stop"));
        var stored = await harness.FindAsync(response.ItemId);

        // Assert
        Assert.Equal(SurveyErrorCode.InvalidAnswer, refused.Error);
        Assert.False(refused.Ended);
        Assert.Contains("q1", refused.Errors.Keys);
        Assert.Equal(SurveyResponseStatus.InProgress, stored.Status);
        Assert.Equal("go", stored.Answers["q1"].Values[0]);
    }

    [Fact]
    public async Task Save_ThatWouldHideALockedAnswer_IsRefused_AndSavesNothing()
    {
        // Arrange: q2 shows only when q1 is "yes".
        await using var harness = await CreateHarnessAsync();
        var survey = await harness.PublishAsync(VisibilityQuiz());
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await SaveAsync(harness, response, ("q1", "yes"));
        await CheckAsync(harness, response, "q2", ("q2", "right"));

        // Act: q1 changes to "no" (which would hide q2) and q3 is answered in the same save.
        var refused = await SaveAsync(harness, response, ("q1", "no"), ("q3", "x"));
        var stored = await harness.FindAsync(response.ItemId);

        // Assert: the whole save is refused, with the error on q1 only.
        Assert.Equal(SurveyErrorCode.InvalidAnswer, refused.Error);
        Assert.Null(refused.SavedUtc);
        Assert.Equal(["q1"], refused.Errors.Keys);
        Assert.Equal([LockMessage], refused.Errors["q1"]);
        Assert.Equal("yes", stored.Answers["q1"].Values[0]);
        Assert.False(stored.Answers.ContainsKey("q3"));
        Assert.True(stored.Answers["q2"].IsLocked);
    }

    [Fact]
    public async Task Finish_ThatWouldHideALockedAnswer_IsRefused_AndKeepsTheResponseOpen()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var survey = await harness.PublishAsync(VisibilityQuiz());
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await SaveAsync(harness, response, ("q1", "yes"));
        await CheckAsync(harness, response, "q2", ("q2", "right"));

        // Act
        var refused = await harness.PostAsync(response, _identity, SurveyPageDirection.Finish, ("q1", "no"), ("q3", "x"));
        var stored = await harness.FindAsync(response.ItemId);

        // Assert: the page comes back with the stored answers, so it shows q2 again.
        Assert.Equal(SurveyErrorCode.InvalidAnswer, refused.Error);
        Assert.False(refused.Ended);
        Assert.Equal(["q1"], refused.Errors.Keys);
        Assert.DoesNotContain("q2", refused.Page.HiddenQuestionIds);
        Assert.Equal("yes", refused.Response.Answers["q1"].Values[0]);
        Assert.Equal(SurveyResponseStatus.InProgress, stored.Status);
        Assert.False(stored.Answers.ContainsKey("q3"));
    }

    [Fact]
    public async Task Back_WithAnAnswerThatWouldHideALockedOne_IsRefused()
    {
        // Arrange: on p2, q2 shows only when q3 (on the same page) is "show".
        await using var harness = await CreateHarnessAsync();
        var q2 = Scored("q2");
        q2.Visibility = RuntimeSurveys.ShowWhen("q3", ConditionOperator.Is, "show");
        var definition = Immediate(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")), RuntimeSurveys.Page("p2", q2, RuntimeSurveys.Choice("q3", "show", "hide"))));
        var survey = await harness.PublishAsync(definition);
        var response = (await harness.StartAsync(survey, "u1")).Response;

        var onP2 = (await harness.PostAsync(response, _identity, SurveyPageDirection.Next, ("q1", "x"))).Response;
        await SaveAsync(harness, onP2, ("q3", "show"));
        await CheckAsync(harness, onP2, "q2", ("q2", "right"));

        // Act: going back keeps the page's answers as posted, so it can't take "hide" with it.
        var refused = await harness.PostAsync(onP2, _identity, SurveyPageDirection.Back, ("q3", "hide"));
        var stored = await harness.FindAsync(response.ItemId);

        // Assert
        Assert.Equal(SurveyErrorCode.InvalidAnswer, refused.Error);
        Assert.Equal(["q3"], refused.Errors.Keys);
        Assert.Equal("p2", stored.CurrentPageId);
        Assert.Equal("show", stored.Answers["q3"].Values[0]);
    }

    [Fact]
    public async Task Save_ThatWouldHideALockedAnswerThroughACalculatedVariable_IsRefused()
    {
        // Arrange: the calculated variable "flag" copies q1, and q2 shows only when it is "yes".
        await using var harness = await CreateHarnessAsync();
        var q2 = Scored("q2");
        q2.Visibility = RuntimeSurveys.ShowWhen(SurveyConditionSources.VariablePrefix + "flag", ConditionOperator.Is, "yes");
        var definition = Immediate(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Choice("q1", "yes", "no"), q2), RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q3"))));
        definition.Variables.Add(new SurveyVariable { Name = "flag", Kind = VariableKind.Calculated, Formula = "[q1]", DataType = global::OrchardCore.DataSources.DataFieldType.Text });
        var survey = await harness.PublishAsync(definition);
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await SaveAsync(harness, response, ("q1", "yes"));
        var check = await CheckAsync(harness, response, "q2", ("q2", "right"));

        // Act
        var refused = await SaveAsync(harness, response, ("q1", "no"));
        var stored = await harness.FindAsync(response.ItemId);

        // Assert
        Assert.True(check.Succeeded, string.Join(", ", check.Errors));
        Assert.Equal(SurveyErrorCode.InvalidAnswer, refused.Error);
        Assert.Equal(["q1"], refused.Errors.Keys);
        Assert.Equal("yes", stored.Answers["q1"].Values[0]);
        Assert.Equal("yes", stored.Variables["flag"]);
    }

    [Fact]
    public async Task Check_OfAnAnswerThatWouldHideALockedOne_IsRefused_AndLocksNothing()
    {
        // Arrange: q2 shows only when q1 is "yes", and q1 can be checked too.
        await using var harness = await CreateHarnessAsync();
        var survey = await harness.PublishAsync(VisibilityQuiz());
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await SaveAsync(harness, response, ("q1", "yes"));
        await CheckAsync(harness, response, "q2", ("q2", "right"));

        // Act
        var refused = await CheckAsync(harness, response, "q1", ("q1", "no"));
        var stored = await harness.FindAsync(response.ItemId);

        // Assert
        Assert.Equal(SurveyErrorCode.InvalidAnswer, refused.Error);
        Assert.Equal([LockMessage], refused.Errors);
        Assert.Equal("yes", stored.Answers["q1"].Values[0]);
        Assert.False(stored.Answers["q1"].IsLocked);
        Assert.True(stored.Answers["q2"].IsLocked);
    }

    [Fact]
    public async Task Evaluate_SaysWhichChangedAnswerWouldHideALockedOne()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var survey = await harness.PublishAsync(VisibilityQuiz());
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await SaveAsync(harness, response, ("q1", "yes"));
        await CheckAsync(harness, response, "q2", ("q2", "right"));

        // Act
        var hiding = await EvaluateAsync(harness, response, ("q1", "no"), ("q3", "x"));
        var keeping = await EvaluateAsync(harness, response, ("q1", "yes"), ("q3", "x"));

        // Assert
        Assert.True(hiding.Succeeded);
        Assert.Equal(["q1"], hiding.Errors.Keys);
        Assert.Empty(keeping.Errors);
    }

    [Fact]
    public async Task ApiPagePost_ThatWouldHideALockedAnswer_IsAValidationProblem()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var survey = await harness.PublishAsync(VisibilityQuiz());
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await SaveAsync(harness, response, ("q1", "yes"));
        await CheckAsync(harness, response, "q2", ("q2", "right"));

        // Act
        var result = await harness.RunAsync(services => SurveysApiEndpoints.CompleteAsync(
            response.ItemId,
            new SubmitPageInput { Answers = new JsonObject { ["q1"] = "no", ["q3"] = "x" } },
            new DefaultHttpContext { RequestServices = services, User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u1")], "Api")) },
            Allow(),
            services.GetRequiredService<ISurveyResponseService>()));

        var stored = await harness.FindAsync(response.ItemId);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result);
        var details = Assert.IsType<HttpValidationProblemDetails>(problem.ProblemDetails);

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal([LockMessage], details.Errors["q1"]);
        Assert.Equal(SurveyResponseStatus.InProgress, stored.Status);
        Assert.Equal("yes", stored.Answers["q1"].Values[0]);
    }

    [Fact]
    public async Task ALockedAnswerTheAuthorsTimeRuleHides_StillCounts_AndDoesNotBlockTheRespondent()
    {
        // Arrange: q2 shows only before 16:00 UTC (the harness starts at 15:00).
        await using var harness = await CreateHarnessAsync();
        var q2 = Scored("q2");
        q2.Visibility = new SurveyVisibility { Mode = VisibilityMode.ShowWhen, Formula = "HOUR(NOW()) < 16" };
        var q1 = RuntimeSurveys.Choice("q1", "yes", "no");
        q1.Scoring = new SurveyQuestionScoring { Points = 0 };
        var definition = Immediate(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1, q2, Scored("q3"))));
        var survey = await harness.PublishAsync(definition);
        var response = (await harness.StartAsync(survey, "u1")).Response;

        var check = await CheckAsync(harness, response, "q2", ("q2", "right"));

        // Act: the time rule hides q2 without any change of the respondent's; they save and finish.
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddHours(1).AddMinutes(30);
        var saved = await SaveAsync(harness, response, ("q1", "no"));
        var finish = await harness.PostAsync(response, _identity, SurveyPageDirection.Finish, ("q1", "no"), ("q3", "right"));
        var stored = await harness.FindAsync(response.ItemId);

        // Assert: the locked answer still counts, with its points and its most points.
        Assert.True(check.Succeeded, string.Join(", ", check.Errors));
        Assert.True(saved.Succeeded, saved.Error.ToString());
        Assert.True(finish.Ended, finish.Error + " " + string.Join(", ", finish.Errors.SelectMany(pair => pair.Value)));
        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.True(stored.Answers["q2"].IsLocked);
        Assert.Equal(4m, stored.Score);
        Assert.Equal(4m, stored.MaxScore);
    }

    [Fact]
    public async Task TimedPage_WhoseInvalidSavedAnswerKeepsALockedOneShown_KeepsItWhenTheTimeIsUp()
    {
        // Arrange: p1 is timed; q2 on p2 shows only while n (on p1) has an answer.
        await using var harness = await CreateHarnessAsync();
        var page1 = RuntimeSurveys.Page("p1", RuntimeSurveys.Number("n"));
        page1.TimeLimitSeconds = 60;
        var q2 = Scored("q2");
        q2.Visibility = RuntimeSurveys.ShowWhen("n", ConditionOperator.IsNotEmpty);
        var definition = Immediate(RuntimeSurveys.Definition(page1, RuntimeSurveys.Page("p2", q2), RuntimeSurveys.Page("p3", RuntimeSurveys.Text("q3"))));
        definition.Timing.GraceSeconds = 0;
        var survey = await harness.PublishAsync(definition);
        var response = (await harness.StartAsync(survey, "u1")).Response;

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(10);
        var onP2 = (await harness.PostAsync(response, _identity, SurveyPageDirection.Next, ("n", "5"))).Response;
        await CheckAsync(harness, onP2, "q2", ("q2", "right"));

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(20);
        var backOnP1 = (await harness.PostAsync(onP2, _identity, SurveyPageDirection.Back)).Response;
        var saved = await SaveAsync(harness, backOnP1, ("n", "not a number"));

        // Act: the page's time is up; dropping the invalid n would hide q2, so it stays as saved and the page moves on.
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(65);
        var next = await harness.PostAsync(backOnP1, _identity, SurveyPageDirection.Next);
        var stored = await harness.FindAsync(response.ItemId);

        // Assert
        Assert.True(saved.Succeeded, saved.Error.ToString());
        Assert.True(next.Succeeded, next.Error + " " + string.Join(", ", next.Errors.SelectMany(pair => pair.Value)));
        Assert.Equal("p2", stored.CurrentPageId);
        Assert.Equal("not a number", stored.Answers["n"].Values[0]);
        Assert.True(stored.Answers["q2"].IsLocked);
    }

    [Fact]
    public async Task Changes_ThatKeepEveryLockedAnswer_AreSaved()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var survey = await harness.PublishAsync(VisibilityQuiz());
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await SaveAsync(harness, response, ("q1", "yes"));
        await CheckAsync(harness, response, "q2", ("q2", "right"));

        // Act
        var saved = await SaveAsync(harness, response, ("q1", "yes"), ("q3", "x"));
        var stored = await harness.FindAsync(response.ItemId);

        // Assert
        Assert.True(saved.Succeeded);
        Assert.Empty(saved.Errors);
        Assert.Equal("x", stored.Answers["q3"].Values[0]);
    }

    private static Task<RuntimeTestHarness> CreateHarnessAsync()
        => RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddScoped<ISurveyScoringEngine, SurveyScoringEngine>();
            services.AddSurveyControlScorer<RightChoiceScorer>();
        });

    // p1: q1 "a" goes on to branch A (p2), "b" jumps to branch B (p3); p2 jumps past p3 to p4.
    private static SurveyDefinition BranchQuiz()
    {
        var q1 = RuntimeSurveys.Choice("q1", "a", "b");
        q1.Scoring = new SurveyQuestionScoring { Points = 0 };

        var page1 = RuntimeSurveys.Page("p1", q1);
        page1.Branches.Add(new SurveyBranch { Id = "toB", Rule = Is("q1", "b"), TargetKind = BranchTargetKind.Page, TargetId = "p3" });

        var page2 = RuntimeSurveys.Page("p2", Scored("qa"));
        page2.Branches.Add(new SurveyBranch { Id = "pastB", TargetKind = BranchTargetKind.Page, TargetId = "p4" });

        return Immediate(RuntimeSurveys.Definition(page1, page2, RuntimeSurveys.Page("p3", Scored("qb")), RuntimeSurveys.Page("p4", Scored("q4", points: 0))));
    }

    // p1: q1, then q2 that shows only when q1 is "yes", then q3; p2 follows.
    private static SurveyDefinition VisibilityQuiz()
    {
        var q1 = RuntimeSurveys.Choice("q1", "yes", "no");
        q1.Scoring = new SurveyQuestionScoring { Points = 0 };

        var q2 = Scored("q2");
        q2.Visibility = RuntimeSurveys.ShowWhen("q1", ConditionOperator.Is, "yes");

        return Immediate(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1, q2, RuntimeSurveys.Text("q3")), RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q4"))));
    }

    private static SurveyQuestion Scored(string id, decimal points = 2)
    {
        var question = RuntimeSurveys.Choice(id, "right", "wrong");
        question.Scoring = new SurveyQuestionScoring { Points = points, CorrectChoiceIds = ["right"] };

        return question;
    }

    private static SurveyDefinition Immediate(SurveyDefinition definition)
    {
        definition.Scoring.Enabled = true;
        definition.Scoring.FeedbackMode = FeedbackMode.Immediate;

        return definition;
    }

    private static ConditionRule Is(string source, string value)
        => new() { Conditions = [new Condition { Source = source, Operator = ConditionOperator.Is, Values = [value] }] };

    private static Task<SurveySaveResult> SaveAsync(RuntimeTestHarness harness, SurveyResponse response, params (string Key, string Value)[] fields)
        => harness.ResponsesAsync(responses => responses.SaveAsync(new SavePageRequest
        {
            ResponseId = response.ItemId,
            Identity = _identity,
            PageId = response.CurrentPageId,
            Form = RuntimeTestHarness.Form(fields),
        }));

    private static Task<SurveyCheckResult> CheckAsync(RuntimeTestHarness harness, SurveyResponse response, string questionId, params (string Key, string Value)[] fields)
        => harness.ResponsesAsync(responses => responses.CheckAnswerAsync(new CheckAnswerRequest
        {
            ResponseId = response.ItemId,
            Identity = _identity,
            QuestionId = questionId,
            Form = RuntimeTestHarness.Form(fields),
        }));

    private static Task<SurveyEvaluateResult> EvaluateAsync(RuntimeTestHarness harness, SurveyResponse response, params (string Key, string Value)[] fields)
        => harness.ResponsesAsync(responses => responses.EvaluateAsync(new EvaluatePageRequest
        {
            ResponseId = response.ItemId,
            Identity = _identity,
            Form = RuntimeTestHarness.Form(fields),
        }));

    private static IAuthorizationService Allow()
    {
        var service = new Mock<IAuthorizationService>();

        service.Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        service.Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return service.Object;
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
