using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Scoring;

public sealed class SurveyScoringEngineTests
{
    private readonly ScoringHiddenResolver _hidden = new();

    [Fact]
    public void Score_WhenScoringIsOff_LeavesTheResponseUnscored()
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 2m));
        definition.Scoring.Enabled = false;
        var response = ScoringDefinitions.Response(("q1", 2m));
        response.Score = 5;

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(GradingStatus.NotScored, response.Grading);
        Assert.Null(response.Score);
        Assert.Null(response.Percent);
    }

    [Fact]
    public void Score_SumsThePointsAndThePercent()
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 2m), ("q2", 3m));
        var response = ScoringDefinitions.Response(("q1", 2m), ("q2", 1m));

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(3m, response.Score);
        Assert.Equal(5m, response.MaxScore);
        Assert.Equal(60m, response.Percent);
        Assert.Equal(GradingStatus.Graded, response.Grading);
        Assert.Equal(1m, response.Answers["q2"].AutoScore);
        Assert.Equal(3m, response.Answers["q2"].MaxPoints);
    }

    [Fact]
    public void Score_WhenAQuestionIsUnanswered_CountsZeroAndItsPoints()
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 2m), ("q2", 2m));
        var response = ScoringDefinitions.Response(("q1", 2m));

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(2m, response.Score);
        Assert.Equal(4m, response.MaxScore);
        Assert.Equal(GradingStatus.Graded, response.Grading);
    }

    [Fact]
    public void Score_WhenAnAnswerNeedsAGrader_IsPendingAndDoesNotPass()
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 2m), ("q2", 2m));
        definition.Scoring.PassMarkPercent = 10;
        var response = ScoringDefinitions.Response(("q1", 2m));
        response.Answers["q2"] = new SurveyAnswer { Values = ["manual"] };

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(GradingStatus.Pending, response.Grading);
        Assert.Null(response.Passed);
        Assert.Null(response.Answers["q2"].AutoScore);
    }

    [Fact]
    public void Score_KeepsTheGradersScore()
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 2m), ("q2", 4m));
        var response = ScoringDefinitions.Response(("q1", 2m));
        response.Answers["q2"] = new SurveyAnswer { Values = ["manual"], ManualScore = 3 };

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(GradingStatus.Graded, response.Grading);
        Assert.Equal(5m, response.Score);
        Assert.Equal(3m, response.Answers["q2"].ManualScore);
    }

    [Fact]
    public void Score_LeavesHiddenQuestionsOutOfTheMostPoints()
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 2m), ("q2", 8m));
        var response = ScoringDefinitions.Response(("q1", 2m));
        _hidden.Hidden.Add("q2");

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(2m, response.MaxScore);
        Assert.Equal(100m, response.Percent);
    }

    [Fact]
    public void Score_LeavesQuestionsOfSkippedPagesOut()
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 2m), ("q2", 2m));
        var response = ScoringDefinitions.Response(("q1", 2m));
        response.Plan.QuestionOrder["s1"] = ["q1"];
        response.Plan.QuestionOrder["s2"] = ["q2"];
        response.PagePath = ["s1"];

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(2m, response.MaxScore);
    }

    [Fact]
    public void Score_CountsALockedAnswerWhoseQuestionLogicHidLater_InThePointsAndTheMostPoints()
    {
        // Arrange: q2 was checked with immediate feedback (locked), then logic hid it.
        var definition = ScoringDefinitions.Quiz(("q1", 2m), ("q2", 8m));
        var response = ScoringDefinitions.Response(("q1", 2m), ("q2", 6m));
        response.Answers["q2"].CheckedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _hidden.Hidden.Add("q2");

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(8m, response.Score);
        Assert.Equal(10m, response.MaxScore);
        Assert.Equal(80m, response.Percent);
        Assert.Equal(8m, response.Answers["q2"].MaxPoints);
    }

    [Fact]
    public void Score_LeavesAnUnlockedAnswerWhoseQuestionLogicHid_Out()
    {
        // Arrange: q2 kept a value but was never checked, and logic hid it.
        var definition = ScoringDefinitions.Quiz(("q1", 2m), ("q2", 8m));
        var response = ScoringDefinitions.Response(("q1", 2m), ("q2", 6m));
        _hidden.Hidden.Add("q2");

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(2m, response.Score);
        Assert.Equal(2m, response.MaxScore);
        Assert.Equal(100m, response.Percent);
    }

    [Fact]
    public void Score_CountsALockedAnswerOnAPageABranchSkippedLater()
    {
        // Arrange: the respondent checked q2 on s2, went back and took a branch that skips s2.
        var definition = ScoringDefinitions.Quiz(("q1", 2m), ("q2", 2m), ("q3", 4m));
        var response = ScoringDefinitions.Response(("q1", 2m), ("q2", 0m), ("q3", 4m));
        response.Answers["q2"].CheckedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        response.Plan.QuestionOrder["s1"] = ["q1"];
        response.Plan.QuestionOrder["s2"] = ["q2"];
        response.Plan.QuestionOrder["s3"] = ["q3"];
        response.PagePath = ["s1"];

        // Act
        var counted = SurveyScoringEngine.CountedQuestions(definition, response, new HashSet<string>(StringComparer.Ordinal)).Select(question => question.Id);
        Engine().Score(definition, response);

        // Assert: q3 (unlocked, on a skipped page) stays out.
        Assert.Equal(["q1", "q2"], counted);
        Assert.Equal(2m, response.Score);
        Assert.Equal(4m, response.MaxScore);
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void Score_ComparesThePercentWithThePassMark(int passMark, bool passed)
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 2m), ("q2", 3m));
        definition.Scoring.PassMarkPercent = passMark;
        var response = ScoringDefinitions.Response(("q1", 2m), ("q2", 1m));

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(passed, response.Passed);
    }

    [Fact]
    public void Score_PicksTheHighestBandReached_AndItsPassingWins()
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 10m));
        definition.Scoring.PassMarkPercent = 90;
        definition.ScoreBands =
        [
            new ScoreBand { Id = "low", MinPercent = 0 },
            new ScoreBand { Id = "good", MinPercent = 50, Passing = true },
            new ScoreBand { Id = "top", MinPercent = 90 },
        ];
        var response = ScoringDefinitions.Response(("q1", 7m));

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal("good", response.ScoreBandId);
        Assert.True(response.Passed);
    }

    [Fact]
    public void Score_AddsPointsToTheirCategories_AndAppliesTheScoreFormula()
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 2m, "c1"), ("q2", 2m, "c1"), ("q3", 2m, "c2"));
        definition.ScoreCategories = [new ScoreCategory { Id = "c1", Name = "skill" }, new ScoreCategory { Id = "c2", Name = "other" }];
        definition.Scoring.ScoreFormula = "[skill] * 2";
        var response = ScoringDefinitions.Response(("q1", 1m), ("q2", 2m), ("q3", 2m));

        // Act
        new SurveyScoringEngine(new ScoringFakeRegistry(), new ScoringFormulaEvaluator(), _hidden).Score(definition, response);

        // Assert
        Assert.Equal(3m, response.CategoryScores["c1"]);
        Assert.Equal(2m, response.CategoryScores["c2"]);
        Assert.Equal(6m, response.Score);
    }

    [Fact]
    public void Score_RoundsToTheDecimalsOfTheSurvey()
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 3m));
        definition.Scoring.RoundingDecimals = 1;
        var response = ScoringDefinitions.Response(("q1", 1m));

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(33.3m, response.Percent);
    }

    [Fact]
    public void Score_SkipsQuestionsWithoutAScorer()
    {
        // Arrange
        var definition = ScoringDefinitions.Quiz(("q1", 2m));
        definition.Pages[0].Questions.Add(new SurveyQuestion { Id = "text", ControlType = "TextBlock" });
        var response = ScoringDefinitions.Response(("q1", 2m));

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(2m, response.MaxScore);
    }

    [Fact]
    public void R12L16_Score_DecidesThePassOnTheUnroundedPercent()
    {
        // Arrange: 139 of 200 is 69.5%, shown as 70% at 0 decimals; the pass mark is 70%.
        var definition = ScoringDefinitions.Quiz(("q1", 200m));
        definition.Scoring.RoundingDecimals = 0;
        definition.Scoring.PassMarkPercent = 70;
        definition.ScoreBands = [new ScoreBand { Id = "good", MinPercent = 70 }];
        var response = ScoringDefinitions.Response(("q1", 139m));

        // Act
        Engine().Score(definition, response);

        // Assert
        Assert.Equal(70m, response.Percent);
        Assert.False(response.Passed);
        Assert.Null(response.ScoreBandId);
    }

    private SurveyScoringEngine Engine()
        => new(new ScoringFakeRegistry(), new NullSurveyScoreFormulaEvaluator(), _hidden);
}
