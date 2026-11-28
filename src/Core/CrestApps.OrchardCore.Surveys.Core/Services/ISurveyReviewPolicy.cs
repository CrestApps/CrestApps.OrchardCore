using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Decides what a respondent may see of a response: the score, their answers, the right answers and the feedback,
/// following the survey's review settings and the moment (during, right after, later while open, after close).
/// </summary>
public interface ISurveyReviewPolicy
{
    /// <summary>
    /// Gets the moment of a response's review. The moment after close starts when staff close the survey, or once its
    /// closing date and the grace past it (<see cref="SurveyTimingSettings.GraceSeconds"/>) are over, when no other
    /// respondent's last page can still be taken.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="definition">The definition of the response's version, or <see langword="null"/>.</param>
    /// <param name="response">The response.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>The moment.</returns>
    SurveyReviewMoment GetMoment(Survey survey, SurveyDefinition definition, SurveyResponse response, DateTime utcNow);

    /// <summary>
    /// Gets what a respondent may see of a whole response now.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="definition">The definition of the response's version.</param>
    /// <param name="response">The response.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>What shows.</returns>
    ReviewShows GetAllowed(Survey survey, SurveyDefinition definition, SurveyResponse response, DateTime utcNow);

    /// <summary>
    /// Gets what a respondent may see of one answer now. While the response is in progress only answers checked with
    /// immediate feedback show anything.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="definition">The definition of the response's version.</param>
    /// <param name="response">The response.</param>
    /// <param name="questionId">The question.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>What shows.</returns>
    ReviewShows GetAllowedFor(Survey survey, SurveyDefinition definition, SurveyResponse response, string questionId, DateTime utcNow);
}
