using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.Scoring;

public sealed class SurveyReviewPolicyTests
{
    private static readonly DateTime _completed = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly SurveyReviewPolicy _policy = new();

    [Theory]
    [InlineData(1, SurveyReviewMoment.ImmediatelyAfter)]
    [InlineData(10, SurveyReviewMoment.LaterWhileOpen)]
    public void GetMoment_FollowsTheTimeSinceFinishing(int minutes, SurveyReviewMoment expected)
    {
        // Act
        var moment = _policy.GetMoment(OpenSurvey(), Quiz(), Completed(), _completed.AddMinutes(minutes));

        // Assert
        Assert.Equal(expected, moment);
    }

    [Fact]
    public void GetMoment_AfterTheCloseDate_IsAfterClose()
    {
        // Arrange
        var survey = OpenSurvey();
        survey.Access.ClosesUtc = _completed.AddHours(1);

        // Act
        var moment = _policy.GetMoment(survey, Quiz(), Completed(), _completed.AddHours(2));

        // Assert
        Assert.Equal(SurveyReviewMoment.AfterClose, moment);
    }

    [Fact]
    public void GetAllowed_UsesTheSettingsOfEachMoment()
    {
        // Arrange
        var definition = Quiz();
        definition.Review.ImmediatelyAfter = ReviewShows.All;
        definition.Review.LaterWhileOpen = ReviewShows.Score;
        definition.Review.AfterClose = ReviewShows.Score | ReviewShows.RightAnswers | ReviewShows.GeneralFeedback;
        var survey = OpenSurvey();

        // Act and assert
        Assert.Equal(ReviewShows.All, _policy.GetAllowed(survey, definition, Completed(), _completed.AddMinutes(1)));
        Assert.Equal(ReviewShows.Score, _policy.GetAllowed(survey, definition, Completed(), _completed.AddMinutes(10)));

        survey.Status = SurveyStatus.Closed;
        Assert.Equal(ReviewShows.Score | ReviewShows.RightAnswers | ReviewShows.GeneralFeedback, _policy.GetAllowed(survey, definition, Completed(), _completed.AddMinutes(10)));
    }

    [Fact]
    public void GetAllowed_WhileGradingIsPending_ShowsOnlyTheAnswers()
    {
        // Arrange
        var definition = Quiz();
        definition.Review.ImmediatelyAfter = ReviewShows.All;
        var response = Completed();
        response.Grading = GradingStatus.Pending;

        // Act
        var shows = _policy.GetAllowed(OpenSurvey(), definition, response, _completed.AddMinutes(1));

        // Assert
        Assert.Equal(ReviewShows.Responses, shows);
    }

    [Fact]
    public void GetAllowed_ForAnUnscoredSurvey_ShowsOnlyTheAnswers()
    {
        // Arrange
        var definition = new SurveyDefinition();
        definition.Review.ImmediatelyAfter = ReviewShows.All;
        var response = Completed();
        response.Grading = GradingStatus.NotScored;

        // Act
        var shows = _policy.GetAllowed(OpenSurvey(), definition, response, _completed.AddMinutes(1));

        // Assert
        Assert.Equal(ReviewShows.Responses, shows);
    }

    [Fact]
    public void GetAllowed_WhenTheScoreIsHiddenFromRespondents_LeavesItOut()
    {
        // Arrange
        var definition = Quiz();
        definition.Scoring.ShowScoreToRespondent = false;
        definition.Review.ImmediatelyAfter = ReviewShows.All;

        // Act
        var shows = _policy.GetAllowed(OpenSurvey(), definition, Completed(), _completed.AddMinutes(1));

        // Assert
        Assert.False(shows.HasFlag(ReviewShows.Score));
        Assert.True(shows.HasFlag(ReviewShows.RightAnswers));
    }

    [Theory]
    [InlineData(SurveyResponseStatus.Voided)]
    [InlineData(SurveyResponseStatus.Abandoned)]
    public void GetAllowed_ForVoidedOrAbandonedResponses_ShowsNothing(SurveyResponseStatus status)
    {
        // Arrange
        var definition = Quiz();
        definition.Review.LaterWhileOpen = ReviewShows.All;
        var response = Completed();
        response.Status = status;

        // Act
        var shows = _policy.GetAllowed(OpenSurvey(), definition, response, _completed.AddDays(1));

        // Assert
        Assert.Equal(ReviewShows.None, shows);
    }

    [Fact]
    public void GetAllowedFor_WhileInProgress_ShowsOnlyCheckedAnswers()
    {
        // Arrange
        var definition = Quiz();
        definition.Scoring.FeedbackMode = FeedbackMode.Immediate;
        definition.Review.During = ReviewShows.Score | ReviewShows.Feedback;
        var response = new SurveyResponse { Status = SurveyResponseStatus.InProgress };
        response.Answers["checked"] = new SurveyAnswer { CheckedUtc = _completed };
        response.Answers["open"] = new SurveyAnswer();

        // Act and assert
        Assert.Equal(ReviewShows.Score | ReviewShows.Feedback, _policy.GetAllowedFor(OpenSurvey(), definition, response, "checked", _completed));
        Assert.Equal(ReviewShows.None, _policy.GetAllowedFor(OpenSurvey(), definition, response, "open", _completed));
        Assert.Equal(ReviewShows.None, _policy.GetAllowed(OpenSurvey(), definition, response, _completed));
    }

    private static Survey OpenSurvey()
        => new() { Status = SurveyStatus.Open };

    private static SurveyDefinition Quiz()
        => new() { Scoring = new SurveyScoringSettings { Enabled = true } };

    private static SurveyResponse Completed()
        => new() { Status = SurveyResponseStatus.Completed, Grading = GradingStatus.Graded, CompletedUtc = _completed };
}
