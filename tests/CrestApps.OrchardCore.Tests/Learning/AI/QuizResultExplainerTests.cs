using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Learning.AI;

/// <summary>
/// The explain-my-result tool shows only what the quiz's review settings let the learner see: a review that shows the
/// answers but hides the score never gives the score away.
/// </summary>
public sealed class QuizResultExplainerTests
{
    [Fact]
    public void Describe_ReviewShowsAnswersButHidesTheScore_LeavesScorePercentAndPassedOut()
    {
        // Arrange
        var response = Response();

        // Act
        var explanation = QuizResultExplainer.Describe(response, new SurveyDefinition(), "Quiz", ReviewShows.Responses, registry: null);

        // Assert
        Assert.True(explanation.ReviewAvailable);
        Assert.Null(explanation.Score);
        Assert.Null(explanation.MaxScore);
        Assert.Null(explanation.Percent);
        Assert.Null(explanation.Passed);
        Assert.Null(explanation.PassMark);
        Assert.Contains("hide the score", explanation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_ReviewShowsTheScore_GivesTheScore()
    {
        // Arrange
        var response = Response();

        // Act
        var explanation = QuizResultExplainer.Describe(response, new SurveyDefinition(), "Quiz", ReviewShows.Responses | ReviewShows.Score, registry: null);

        // Assert
        Assert.Equal(8m, explanation.Score);
        Assert.Equal(80m, explanation.Percent);
        Assert.True(explanation.Passed);
    }

    private static SurveyResponse Response()
        => new()
        {
            ItemId = "r1",
            Status = SurveyResponseStatus.Completed,
            Score = 8m,
            MaxScore = 10m,
            Percent = 80m,
            Passed = true,
        };
}
