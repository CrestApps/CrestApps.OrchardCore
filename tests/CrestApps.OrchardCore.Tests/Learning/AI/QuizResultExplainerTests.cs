using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

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

    [Theory]
    [InlineData(ReviewShows.Responses | ReviewShows.Score, false)]
    [InlineData(ReviewShows.Responses | ReviewShows.Score | ReviewShows.Feedback, true)]
    public void Describe_GradersWords_FollowTheFeedbackSetting(ReviewShows shows, bool expected)
    {
        // Arrange: a graded answer with a grader's comment, and the grader's overall feedback.
        var definition = new SurveyDefinition();
        definition.Pages.Add(new SurveyPage { Id = "p1", Questions = [new SurveyQuestion { Id = "q1", Name = "q1", Title = "Q1", ControlType = AuthoringSurveyHarness.TextControl }] });
        var response = Response();
        response.Feedback = "Overall: revise chapter 2.";
        response.Answers["q1"] = new SurveyAnswer { Values = ["my answer"], ManualScore = 1m, MaxPoints = 2m, GraderComment = "The key is X." };

        // Act
        var explanation = QuizResultExplainer.Describe(response, definition, "Quiz", shows, AuthoringSurveyHarness.CreateRegistry());

        // Assert
        var question = Assert.Single(explanation.Questions);
        Assert.Equal(expected, explanation.Feedback is not null);
        Assert.Equal(expected, question.Feedback?.Contains("The key is X.", StringComparison.Ordinal) == true);
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
