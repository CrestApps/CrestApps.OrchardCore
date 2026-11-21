using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Grants, lists and revokes accommodations.
/// </summary>
public interface ISurveyOverrideService
{
    /// <summary>
    /// Grants an accommodation. A reason is required.
    /// </summary>
    /// <param name="surveyOverride">The accommodation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The error, or <see cref="SurveyErrorCode.None"/>.</returns>
    Task<SurveyErrorCode> GrantAsync(SurveyOverride surveyOverride, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the accommodations of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accommodations.</returns>
    Task<IReadOnlyList<SurveyOverride>> ListAsync(string surveyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an accommodation.
    /// </summary>
    /// <param name="overrideId">The accommodation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when it was revoked.</returns>
    Task<bool> RevokeAsync(string overrideId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most generous accommodation that applies to a user in a context, or <see langword="null"/>.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="userId">The user, or <see langword="null"/>.</param>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The combined accommodation, or <see langword="null"/>.</returns>
    Task<SurveyOverride> FindEffectiveAsync(string surveyId, string userId, RecordRef? context, CancellationToken cancellationToken = default);
}
