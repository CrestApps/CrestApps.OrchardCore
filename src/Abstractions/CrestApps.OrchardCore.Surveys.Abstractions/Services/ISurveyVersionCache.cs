using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Reads immutable survey versions, cached in memory by id for the tenant.
/// </summary>
public interface ISurveyVersionCache
{
    /// <summary>
    /// Gets a version by id.
    /// </summary>
    /// <param name="versionId">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version, or <see langword="null"/>.</returns>
    Task<SurveyVersion> GetAsync(string versionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the published version of a survey.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version, or <see langword="null"/> before the first publish.</returns>
    Task<SurveyVersion> GetPublishedAsync(Survey survey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the versions of a survey, newest first.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The versions.</returns>
    Task<IReadOnlyList<SurveyVersion>> ListAsync(string surveyId, CancellationToken cancellationToken = default);
}
