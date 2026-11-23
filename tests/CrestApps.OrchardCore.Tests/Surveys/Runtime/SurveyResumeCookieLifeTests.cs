using CrestApps.OrchardCore.Surveys.Controllers;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// When a guest's response ends, the resume cookie keeps its full life while a later review or a reopen still needs it.
/// </summary>
public sealed class SurveyResumeCookieLifeTests
{
    [Fact]
    public void AReviewShownOnlyAfterClose_OnAnOpenSurvey_KeepsTheCookie()
    {
        // Arrange: the answers show only after the survey closes, which is more than a day away (or whenever staff close it).
        var survey = new Survey { Status = SurveyStatus.Open, Access = new SurveyAccessSettings { ClosesUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc) } };
        var definition = new SurveyDefinition { Review = new SurveyReviewSettings { LaterWhileOpen = ReviewShows.None, AfterClose = ReviewShows.Responses } };
        var response = new SurveyResponse { Status = SurveyResponseStatus.Completed };

        // Act
        var keeps = SurveyRunnerController.CanReturnLater(survey, definition, response, later: ReviewShows.None);

        // Assert
        Assert.True(keeps);
    }

    [Fact]
    public void NoLaterReview_NoReopen_ShortensTheCookie()
    {
        // Arrange
        var survey = new Survey { Status = SurveyStatus.Open };
        var definition = new SurveyDefinition { Review = new SurveyReviewSettings { LaterWhileOpen = ReviewShows.None, AfterClose = ReviewShows.Score } };
        var response = new SurveyResponse { Status = SurveyResponseStatus.Completed };

        // Act
        var keeps = SurveyRunnerController.CanReturnLater(survey, definition, response, later: ReviewShows.None);

        // Assert
        Assert.False(keeps);
    }

    [Fact]
    public void AClosedSurvey_FollowsItsReviewAfterClose()
    {
        // Arrange: closed already, so the review a day later is the one after close, which the caller passes in.
        var survey = new Survey { Status = SurveyStatus.Closed };
        var definition = new SurveyDefinition { Review = new SurveyReviewSettings { AfterClose = ReviewShows.Responses } };
        var response = new SurveyResponse { Status = SurveyResponseStatus.Completed };

        // Act
        var keepsWithReview = SurveyRunnerController.CanReturnLater(survey, definition, response, later: ReviewShows.Responses);
        var keepsWithout = SurveyRunnerController.CanReturnLater(survey, definition, response, later: ReviewShows.None);

        // Assert
        Assert.True(keepsWithReview);
        Assert.False(keepsWithout);
    }
}
