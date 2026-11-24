using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Scoring;

/// <summary>
/// Decides what a respondent may see of a response, from the review settings of its version, the moment and where
/// grading stands. A score, the right answers and the feedback never show before every question is graded.
/// </summary>
public sealed class SurveyReviewPolicy : ISurveyReviewPolicy
{
    /// <summary>
    /// How long after finishing the "right after" settings apply.
    /// </summary>
    public static readonly TimeSpan ImmediatelyAfterWindow = TimeSpan.FromMinutes(2);

    private const ReviewShows KeyParts = ReviewShows.Score | ReviewShows.RightAnswers | ReviewShows.Feedback | ReviewShows.GeneralFeedback;

    /// <inheritdoc />
    public SurveyReviewMoment GetMoment(Survey survey, SurveyDefinition definition, SurveyResponse response, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Status == SurveyResponseStatus.InProgress)
        {
            return SurveyReviewMoment.During;
        }

        if (SurveyAvailability.IsClosedForReview(survey, definition, utcNow))
        {
            return SurveyReviewMoment.AfterClose;
        }

        return response.CompletedUtc is { } completed && utcNow < completed + ImmediatelyAfterWindow
            ? SurveyReviewMoment.ImmediatelyAfter
            : SurveyReviewMoment.LaterWhileOpen;
    }

    /// <inheritdoc />
    public ReviewShows GetAllowed(Survey survey, SurveyDefinition definition, SurveyResponse response, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Status is SurveyResponseStatus.Voided or SurveyResponseStatus.Abandoned)
        {
            return ReviewShows.None;
        }

        var review = definition?.Review ?? new SurveyReviewSettings();
        var moment = GetMoment(survey, definition, response, utcNow);

        if (moment == SurveyReviewMoment.During)
        {
            // While answering, the whole response shows nothing beyond the answers being given.
            return ReviewShows.None;
        }

        var shows = moment switch
        {
            SurveyReviewMoment.ImmediatelyAfter => review.ImmediatelyAfter,
            SurveyReviewMoment.LaterWhileOpen => review.LaterWhileOpen,
            SurveyReviewMoment.AfterClose => review.AfterClose,
            _ => ReviewShows.None,
        };

        return Restrict(shows, definition, response);
    }

    /// <inheritdoc />
    public ReviewShows GetAllowedFor(Survey survey, SurveyDefinition definition, SurveyResponse response, string questionId, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Status == SurveyResponseStatus.InProgress)
        {
            if (string.IsNullOrEmpty(questionId)
                || !response.Answers.TryGetValue(questionId, out var answer)
                || answer?.CheckedUtc is null
                || definition?.Scoring?.FeedbackMode != FeedbackMode.Immediate)
            {
                return ReviewShows.None;
            }

            var during = definition.Review?.During ?? ReviewShows.None;

            return definition.Scoring.ShowScoreToRespondent ? during : during & ~ReviewShows.Score;
        }

        return GetAllowed(survey, definition, response, utcNow);
    }

    private static ReviewShows Restrict(ReviewShows shows, SurveyDefinition definition, SurveyResponse response)
    {
        var scoring = definition?.Scoring;

        if (scoring is null || !scoring.Enabled || response.Grading == GradingStatus.NotScored)
        {
            // An unscored survey has no score, key or feedback to show.
            return shows & ReviewShows.Responses;
        }

        if (response.Status == SurveyResponseStatus.ScreenedOut || response.Grading == GradingStatus.Pending)
        {
            return shows & ~KeyParts;
        }

        return scoring.ShowScoreToRespondent ? shows : shows & ~ReviewShows.Score;
    }
}
