using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Checks, compares and publishes survey drafts as immutable versions.
/// </summary>
public interface ISurveyPublisher
{
    /// <summary>
    /// Checks a survey's draft and compares it with the published version, without publishing.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The problems and the changes.</returns>
    Task<SurveyPublishPreview> PreviewAsync(Survey survey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a survey's draft as a new version.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="notes">The publish notes.</param>
    /// <param name="actorId">The id of the user who publishes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyPublishResult> PublishAsync(string surveyId, string notes, string actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies an old version's definition into the draft.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The error, or <see cref="SurveyErrorCode.None"/>.</returns>
    Task<SurveyErrorCode> RestoreAsync(string surveyId, string versionId, CancellationToken cancellationToken = default);
}
