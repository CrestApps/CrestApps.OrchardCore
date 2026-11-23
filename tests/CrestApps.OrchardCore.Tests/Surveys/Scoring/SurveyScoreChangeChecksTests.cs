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
    public void GradingChangesGivenScores_OnlyForAGradedResponse(GradingStatus grading, bool expected)
        => Assert.Equal(expected, SurveyScoreChangeChecks.GradingChangesGivenScores(new SurveyResponse { Grading = grading }));
}
