using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Applies the quotas of a survey when responses complete. Every method runs under the survey's completion lock.
/// </summary>
public interface ISurveyQuotaService
{
    /// <summary>
    /// Gets the first full quota that screens out a response that matches it.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="version">The version of the response.</param>
    /// <param name="response">The response.</param>
    /// <param name="state">The logic state of the response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The quota, or <see langword="null"/> when none is full for the response.</returns>
    Task<SurveyQuota> FindFullAsync(Survey survey, SurveyVersion version, SurveyResponse response, SurveyLogicState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a completed response in every quota it matches.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="version">The version of the response.</param>
    /// <param name="response">The response.</param>
    /// <param name="state">The logic state of the response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The quotas that reached their limit with this response, with their counts.</returns>
    Task<IReadOnlyList<(SurveyQuota Quota, int Count)>> RecordAsync(Survey survey, SurveyVersion version, SurveyResponse response, SurveyLogicState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells observers that quotas reached their limit, and closes the survey for quotas that close it. Runs after the
    /// response is saved.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="reached">The quotas that reached their limit, with their counts.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task NotifyReachedAsync(Survey survey, IReadOnlyList<(SurveyQuota Quota, int Count)> reached, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the completed count of each quota of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The counts by quota id.</returns>
    Task<IReadOnlyDictionary<string, int>> GetCountsAsync(string surveyId, CancellationToken cancellationToken = default);
}
