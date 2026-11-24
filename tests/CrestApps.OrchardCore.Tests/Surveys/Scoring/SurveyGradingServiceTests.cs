using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;

namespace CrestApps.OrchardCore.Tests.Surveys.Scoring;

public sealed class SurveyGradingServiceTests : IAsyncLifetime
{
    private readonly ScoringVersionCache _versions = new();
    private readonly ScoringDispatcher _dispatcher = new();
    private readonly TestClock _clock = new(new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc));
    private ScoringStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = await ScoringStore.CreateAsync();

        var definition = ScoringDefinitions.Quiz(("q1", 2m), ("essay", 4m));
        _versions.Versions["version1"] = new SurveyVersion { ItemId = "version1", SurveyId = "survey1", Number = 1, Definition = definition };

        await using var session = _store.Store.CreateSession();
        await new SurveyStore(session).CreateAsync(new Survey { ItemId = "survey1", Name = "quiz", Status = SurveyStatus.Open, Draft = definition }, TestContext.Current.CancellationToken);

        var response = ScoringDefinitions.Response(("q1", 2m));
        response.Answers["essay"] = new SurveyAnswer { Values = ["manual"] };
        new SurveyScoringEngine(new ScoringFakeRegistry(), new NullSurveyScoreFormulaEvaluator(), new NullSurveyHiddenQuestionResolver()).Score(definition, response);
        await new SurveyResponseStore(session).CreateAsync(response, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task GradeAsync_ScoresTheEssay_KeepsTheHistory_AndRaisesGraded()
    {
        // Act
        var result = await RunAsync(service => service.GradeAsync("response1", new Dictionary<string, decimal> { ["essay"] = 3 }, new Dictionary<string, string> { ["essay"] = " Good " }, "Well done", "grader1"));

        // Assert
        Assert.True(result.Succeeded);

        var response = await LoadAsync();
        Assert.Equal(GradingStatus.Graded, response.Grading);
        Assert.Equal(5m, response.Score);
        Assert.Equal("Good", response.Answers["essay"].GraderComment);
        Assert.Equal("Well done", response.Feedback);
        Assert.Equal("grader1", response.GradedBy);
        var change = Assert.Single(response.ScoreHistory);
        Assert.Equal(ScoreChangeKind.Graded, change.Kind);
        Assert.Equal("essay", change.QuestionId);
        Assert.Equal(["Graded"], _dispatcher.Events);
    }

    [Fact]
    public async Task R7M9_GradingTheEssay_LeavesTheAutomaticScoresAutomatic_SoScoreAgainAfterAKeyFixMovesThem()
    {
        // Arrange: the grader grades the essay; the page also posts the automatic question's points unchanged.
        await RunAsync(service => service.GradeAsync("response1", new Dictionary<string, decimal> { ["q1"] = 2, ["essay"] = 3 }, null, null, "grader1"));
        var graded = await LoadAsync();

        // The author fixes q1's key: it is now worth 1 point.
        _versions.Versions["version1"].Definition.Pages[0].Questions[0].Scoring.Points = 1;

        // Act: Score again.
        var result = await RunAsync(service => service.RegradeAsync("response1", "lead", "Key fix"));

        // Assert: the essay keeps the grader's 3; q1 follows the fixed key.
        Assert.True(result.Succeeded);
        Assert.Null(graded.Answers["q1"].ManualScore);
        Assert.Equal(3m, graded.Answers["essay"].ManualScore);
        Assert.Equal("essay", Assert.Single(graded.ScoreHistory).QuestionId);

        var response = await LoadAsync();
        Assert.Equal(1m, response.Answers["q1"].Score);
        Assert.Equal(3m, response.Answers["essay"].Score);
        Assert.Equal(4m, response.Score);
    }

    [Fact]
    public async Task R7M9_PointsThatDifferFromTheAutomaticScore_AreTheGradersScore()
    {
        // Act
        await RunAsync(service => service.GradeAsync("response1", new Dictionary<string, decimal> { ["q1"] = 1, ["essay"] = 3 }, null, null, "lead"));

        // Assert
        var response = await LoadAsync();
        Assert.Equal(1m, response.Answers["q1"].ManualScore);
        Assert.Equal(4m, response.Score);
    }

    [Fact]
    public async Task GradeAsync_RefusesPointsAboveTheMost()
    {
        // Act
        var result = await RunAsync(service => service.GradeAsync("response1", new Dictionary<string, decimal> { ["essay"] = 5 }, null, null, "grader1"));

        // Assert
        Assert.Equal(SurveyErrorCode.InvalidAnswer, result.Error);
        Assert.Equal(GradingStatus.Pending, (await LoadAsync()).Grading);
    }

    [Fact]
    public async Task ChangeScoreAsync_RequiresAReason()
    {
        // Act
        var result = await RunAsync(service => service.ChangeScoreAsync("response1", 1, "grader1", "  "));

        // Assert
        Assert.Equal(SurveyErrorCode.InvalidAnswer, result.Error);
    }

    [Fact]
    public async Task ChangeScoreAsync_AfterGrading_RaisesScoreChanged()
    {
        // Arrange
        await RunAsync(service => service.GradeAsync("response1", new Dictionary<string, decimal> { ["essay"] = 3 }, null, null, "grader1"));
        _dispatcher.Events.Clear();

        // Act
        var result = await RunAsync(service => service.ChangeScoreAsync("response1", 6, "lead", "Appeal upheld"));

        // Assert
        Assert.True(result.Succeeded);

        var response = await LoadAsync();
        Assert.Equal(6m, response.Score);
        Assert.Equal(ScoreChangeKind.Changed, response.ScoreHistory.Last().Kind);
        Assert.Equal("Appeal upheld", response.ScoreHistory.Last().Reason);
        Assert.Equal(["ScoreChanged"], _dispatcher.Events);
    }

    [Fact]
    public async Task RegradeVersionsAsync_UsesTheOtherKey_OnlyForQuestionsOfTheSameType()
    {
        // Arrange
        await RunAsync(service => service.GradeAsync("response1", new Dictionary<string, decimal> { ["essay"] = 4 }, null, null, "grader1"));
        var fixedKey = ScoringDefinitions.Quiz(("q1", 10m), ("essay", 4m));
        fixedKey.Pages[0].Questions[1].ControlType = "Other";
        _versions.Versions["version2"] = new SurveyVersion { ItemId = "version2", SurveyId = "survey1", Number = 2, Definition = fixedKey };

        // Act
        var result = await RunAsync(service => service.RegradeVersionsAsync("survey1", ["version1"], "version2", "lead", "Key fix"));

        // Assert
        Assert.Equal(1, result.Count);
        Assert.Empty(result.SkippedQuestions);

        var response = await LoadAsync();
        Assert.Equal(14m, response.MaxScore);
        Assert.Equal(ScoreChangeKind.Regraded, response.ScoreHistory.Last().Kind);
    }

    [Fact]
    public async Task RegradeVersionsAsync_KeyNamingChoicesTheVersionLacks_KeepsTheQuestionScore_AndReportsIt()
    {
        // Arrange: version 1's question has choices a and b; the key version's copy was imported again with new ids.
        await RunAsync(service => service.GradeAsync("response1", new Dictionary<string, decimal> { ["essay"] = 4 }, null, null, "grader1"));
        var ownQuestion = _versions.Versions["version1"].Definition.Pages[0].Questions[0];
        ownQuestion.Title = "Capital city";
        ownQuestion.Choices = [new SurveyChoice { Id = "a" }, new SurveyChoice { Id = "b" }];
        var reimported = ScoringDefinitions.Quiz(("q1", 10m), ("essay", 8m));
        reimported.Pages[0].Questions[0].Choices = [new SurveyChoice { Id = "x" }, new SurveyChoice { Id = "y" }];
        reimported.Pages[0].Questions[0].Scoring.CorrectChoiceIds = ["x"];
        _versions.Versions["version2"] = new SurveyVersion { ItemId = "version2", SurveyId = "survey1", Number = 2, Definition = reimported };

        // Act
        var result = await RunAsync(service => service.RegradeVersionsAsync("survey1", ["version1"], "version2", "lead", "Key fix"));

        // Assert: q1 keeps its 2 of 2; the essay takes the new key's 8 points as its most and keeps the grader's 4.
        Assert.Equal(1, result.Count);
        var skipped = Assert.Single(result.SkippedQuestions);
        Assert.Equal("q1", skipped.QuestionId);
        Assert.Equal("Capital city", skipped.Title);
        Assert.Equal(1, skipped.VersionNumber);

        var response = await LoadAsync();
        Assert.Equal(6m, response.Score);
        Assert.Equal(10m, response.MaxScore);
        Assert.Equal(2m, response.Answers["q1"].AutoScore);
        Assert.Null(response.Answers["q1"].ManualScore);
    }

    private async Task<T> RunAsync<T>(Func<SurveyGradingService, Task<T>> operation)
    {
        await using var session = _store.Store.CreateSession();
        var service = new SurveyGradingService(
            new SurveyResponseStore(session),
            new SurveyStore(session),
            _versions,
            new SurveyScoringEngine(new ScoringFakeRegistry(), new NullSurveyScoreFormulaEvaluator(), new NullSurveyHiddenQuestionResolver()),
            new ScoringResponseQueries(session),
            new YesSqlSurveyResponseCommitter(session),
            _dispatcher,
            _clock);

        return await operation(service);
    }

    private async Task<SurveyResponse> LoadAsync()
    {
        await using var session = _store.Store.CreateSession();

        return await new SurveyResponseStore(session).FindByIdAsync("response1", TestContext.Current.CancellationToken);
    }
}
