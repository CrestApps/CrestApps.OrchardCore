using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Reads responses for staff pages and consumers.
/// </summary>
public interface ISurveyResponseQueries
{
    /// <summary>
    /// Finds a response by id.
    /// </summary>
    /// <param name="responseId">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, or <see langword="null"/>.</returns>
    Task<SurveyResponse> FindByIdAsync(string responseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of responses, newest first.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total count and the page.</returns>
    Task<(int Count, IReadOnlyList<SurveyResponse> Items)> ListAsync(SurveyResponseQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the responses of a survey, test responses left out.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    Task<int> CountForSurveyAsync(string surveyId, CancellationToken cancellationToken = default);
}
