using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controllers;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// R11-L2: an invitation collector's token is not a public link. A start on it needs an invitation, and the Done page
/// never hands the invitee that token.
/// </summary>
public sealed class InvitationCollectorStartTests
{
    private static readonly Survey _survey = new() { ItemId = "s1", Status = SurveyStatus.Open };

    [Fact]
    public async Task CheckStartAsync_AnInvitationCollectorWithoutAnInvitation_IsRefused()
    {
        // Arrange
        var context = new SurveyAccessContext { Survey = _survey, Collector = Collector(SurveyCollectorKinds.Invitation), UtcNow = DateTime.UtcNow };

        // Act
        var decision = await new InvitationRule().CheckStartAsync(context);

        // Assert
        Assert.Equal(SurveyErrorCode.InvitationInvalid, decision.Error);
    }

    [Fact]
    public async Task CheckStartAsync_AnInvitationCollectorWithItsInvitation_IsAllowed()
    {
        // Arrange
        var context = new SurveyAccessContext
        {
            Survey = _survey,
            Collector = Collector(SurveyCollectorKinds.Invitation),
            Invitation = new SurveyInvitation { ItemId = "i1", SurveyId = "s1", Status = SurveyInvitationStatus.Sent },
            UtcNow = DateTime.UtcNow,
        };

        // Act
        var decision = await new InvitationRule().CheckStartAsync(context);

        // Assert
        Assert.Equal(SurveyErrorCode.None, decision.Error);
    }

    [Theory]
    [InlineData(SurveyCollectorKinds.WebLink, false)]
    [InlineData(SurveyCollectorKinds.Invitation, true)]
    public async Task CheckStartAsync_WithoutAnInvitation_RefusesOnlyAnInvitationCollectorAndNeverATest(string kind, bool isTest)
    {
        // Arrange: a public link starts without an invitation; a staff test of an invitation collector does too.
        var context = new SurveyAccessContext { Survey = _survey, Collector = Collector(kind), IsTest = isTest, UtcNow = DateTime.UtcNow };

        // Act
        var decision = await new InvitationRule().CheckStartAsync(context);

        // Assert
        Assert.Equal(SurveyErrorCode.None, decision.Error);
    }

    [Theory]
    [InlineData(ResultsVisibility.AfterAnswer)]
    [InlineData(ResultsVisibility.Always)]
    public void ResultsPath_ForAnInvitee_IsTheResponsesOwnPageNotTheCollectorsToken(ResultsVisibility visibility)
    {
        // Arrange
        var response = new SurveyResponse { ItemId = "r1", Status = SurveyResponseStatus.Completed };

        // Act
        var path = SurveyRunnerController.ResultsPath(Definition(visibility), Collector(SurveyCollectorKinds.Invitation), response);

        // Assert
        Assert.Equal("~/s/r/r1/results", path);
    }

    [Fact]
    public void ResultsPath_ForAPublicLink_IsItsOwnTotalsPage()
    {
        // Arrange
        var response = new SurveyResponse { ItemId = "r1", Status = SurveyResponseStatus.Completed };

        // Act
        var path = SurveyRunnerController.ResultsPath(Definition(ResultsVisibility.AfterAnswer), Collector(SurveyCollectorKinds.WebLink), response);

        // Assert
        Assert.Equal("~/s/tok-1/results", path);
    }

    [Fact]
    public void ResultsPath_WhenTheSurveyShowsNoTotalsAfterAnswering_IsNull()
    {
        // Act
        var path = SurveyRunnerController.ResultsPath(Definition(ResultsVisibility.Never), Collector(SurveyCollectorKinds.WebLink), new SurveyResponse { ItemId = "r1", Status = SurveyResponseStatus.Completed });

        // Assert
        Assert.Null(path);
    }

    private static SurveyCollector Collector(string kind)
        => new() { ItemId = "c1", SurveyId = "s1", Kind = kind, Token = "tok-1", Status = CollectorStatus.Open };

    private static SurveyDefinition Definition(ResultsVisibility visibility)
        => new() { Review = new SurveyReviewSettings { ResultsVisibility = visibility } };
}
