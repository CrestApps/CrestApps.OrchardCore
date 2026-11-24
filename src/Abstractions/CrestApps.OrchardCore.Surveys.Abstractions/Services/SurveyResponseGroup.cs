using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The joint key a row of an anonymous survey prints: its survey, collector, version and context kind. The small-group
/// rule of an anonymous survey counts its completed responses by this key, so filtering on one of these attributes and
/// reading another can't single out a response.
/// </summary>
/// <param name="SurveyId">The survey.</param>
/// <param name="CollectorId">The collector, or an empty string for none.</param>
/// <param name="VersionNumber">The version number.</param>
/// <param name="ContextKind">The context kind, or an empty string for none.</param>
public readonly record struct SurveyResponseGroup(string SurveyId, string CollectorId, int VersionNumber, string ContextKind)
{
    /// <summary>
    /// Makes the key of a response.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The key.</returns>
    public static SurveyResponseGroup Of(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return Create(response.SurveyId, response.CollectorId, response.VersionNumber, response.Context?.Kind);
    }

    /// <summary>
    /// Makes a key, with an empty string for a missing collector or context kind.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="collectorId">The collector, or <see langword="null"/>.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="contextKind">The context kind, or <see langword="null"/>.</param>
    /// <returns>The key.</returns>
    public static SurveyResponseGroup Create(string surveyId, string collectorId, int versionNumber, string contextKind)
        => new(surveyId ?? string.Empty, collectorId ?? string.Empty, versionNumber, contextKind ?? string.Empty);
}
