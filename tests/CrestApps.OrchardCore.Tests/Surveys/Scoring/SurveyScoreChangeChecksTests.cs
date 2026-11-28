using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Surveys.Scoring;

public sealed class SurveyScoreChangeChecksTests
{
    [Fact]
    public async Task AllowAsync_WithNoCheck_Allows()
        => Assert.True(await new SurveyScoreChangeChecks([]).AllowAsync(new ClaimsPrincipal(), new Survey { ItemId = "s1" }, null, TestContext.Current.CancellationToken));

    [Fact]
    public async Task AllowAsync_OneRefusingCheck_Refuses()
    {
        // Arrange
        var allows = new Mock<ISurveyScoreChangeCheck>();
        allows.Setup(check => check.AllowsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<Survey>(), It.IsAny<SurveyResponse>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var refuses = new Mock<ISurveyScoreChangeCheck>();
        refuses.Setup(check => check.AllowsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<Survey>(), It.IsAny<SurveyResponse>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var allowed = await new SurveyScoreChangeChecks([allows.Object, refuses.Object]).AllowAsync(new ClaimsPrincipal(), new Survey { ItemId = "s1" }, new SurveyResponse(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(allowed);
    }

    [Theory]
    [InlineData(GradingStatus.Graded, true)]
    [InlineData(GradingStatus.Pending, false)]
    [InlineData(GradingStatus.NotScored, false)]
    public void GradingChangesGivenScores_WithNoPoints_OnlyForAGradedResponse(GradingStatus grading, bool expected)
        => Assert.Equal(expected, SurveyScoreChangeChecks.GradingChangesGivenScores(new SurveyResponse { Grading = grading }, null));

    [Theory]
    // The essay waits for a grader: any points are a first grade.
    [InlineData("essay", 3, false)]
    // The choice question was scored 0 by the site: the same 0 changes nothing, full points replace the automatic score.
    [InlineData("choice", 0, false)]
    [InlineData("choice", 1, true)]
    // Another grader gave the second essay 2 points while the attempt still waits for the first essay.
    [InlineData("graded", 2, false)]
    [InlineData("graded", 4, true)]
    // A question left blank, with no answer at all, scores 0.
    [InlineData("blank", 0, false)]
    [InlineData("blank", 1, true)]
    public void R6M6_GradingChangesGivenScores_OnAPendingResponse_WhenAPointChangesAGivenScore(string questionId, int points, bool expected)
    {
        // Arrange
        var response = new SurveyResponse
        {
            Grading = GradingStatus.Pending,
            Answers =
            {
                ["essay"] = new SurveyAnswer { Values = ["My essay"], MaxPoints = 5 },
                ["choice"] = new SurveyAnswer { Values = ["wrong"], AutoScore = 0, MaxPoints = 1 },
                ["graded"] = new SurveyAnswer { Values = ["Second essay"], ManualScore = 2, MaxPoints = 5 },
            },
        };

        // Act
        var changes = SurveyScoreChangeChecks.GradingChangesGivenScores(response, new Dictionary<string, decimal> { [questionId] = points });

        // Assert
        Assert.Equal(expected, changes);
    }
}
