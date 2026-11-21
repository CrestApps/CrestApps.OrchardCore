using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

/// <summary>
/// A quiz course item's result comes from the learner's responses to the quiz's survey in the enrollment: the grade is
/// the quiz's grading method over the graded responses, voided ones do not count, and the pass mark is the survey's.
/// </summary>
public sealed class QuizResultProviderTests
{
    [Theory]
    [InlineData(GradingMethod.Highest, 80)]
    [InlineData(GradingMethod.Latest, 60)]
    [InlineData(GradingMethod.First, 40)]
    [InlineData(GradingMethod.Average, 60)]
    public void Build_CombinesTheGradedResponses_WithTheQuizsGradingMethod(GradingMethod method, decimal expected)
    {
        // Arrange
        var item = Quiz(method);
        var responses = new[]
        {
            Graded("r1", 40, 1),
            Graded("r2", 80, 2),
            Graded("r3", 60, 3),
        };

        // Act
        var result = new QuizResultProvider().Build(item, new Enrollment { ItemId = "e1" }, Definition(passMark: 30), responses);

        // Assert
        Assert.Equal(expected, result.Score);
        Assert.True(result.Passed);
        Assert.True(result.Submitted);
        Assert.True(result.Graded);
        Assert.Equal(3, result.Attempts);
    }

    [Fact]
    public void Build_LeavesOutVoidedResponses_AndComparesWithThePassMark()
    {
        // Arrange
        var voided = Graded("r2", 95, 2);
        voided.Status = SurveyResponseStatus.Voided;

        // Act
        var result = new QuizResultProvider().Build(Quiz(GradingMethod.Highest), new Enrollment { ItemId = "e1" }, Definition(passMark: 70), [Graded("r1", 60, 1), voided]);

        // Assert
        Assert.Equal(60, result.Score);
        Assert.False(result.Passed);
        Assert.Equal(1, result.Attempts);
    }

    [Fact]
    public void Build_AResponseWaitingForAGrader_IsSubmittedButNotGraded()
    {
        // Arrange
        var pending = Graded("r1", null, 1);
        pending.Grading = GradingStatus.Pending;

        // Act
        var result = new QuizResultProvider().Build(Quiz(GradingMethod.Highest), new Enrollment { ItemId = "e1" }, Definition(passMark: 50), [pending]);

        // Assert
        Assert.True(result.Submitted);
        Assert.False(result.Graded);
        Assert.Null(result.Score);
        Assert.Null(result.Passed);
        Assert.True(result.ResetScore);
    }

    private static ContentItem Quiz(GradingMethod method)
    {
        var item = new ContentItem { ContentItemId = "quiz1", ContentType = "QuizLesson" };
        item.Alter<QuizPart>(part =>
        {
            part.SurveyId = "survey1";
            part.GradingMethod = method;
        });

        return item;
    }

    private static SurveyDefinition Definition(decimal passMark)
    {
        var definition = new SurveyDefinition();
        definition.Scoring.Enabled = true;
        definition.Scoring.PassMarkPercent = passMark;

        return definition;
    }

    private static SurveyResponse Graded(string id, decimal? percent, int minute)
        => new()
        {
            ItemId = id,
            SurveyId = "survey1",
            Status = SurveyResponseStatus.Completed,
            Grading = GradingStatus.Graded,
            Percent = percent,
            StartedUtc = new DateTime(2026, 1, 1, 10, minute, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 1, 1, 10, minute, 30, DateTimeKind.Utc),
        };
}
