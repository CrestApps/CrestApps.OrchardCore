using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Finds the version a response runs on: a published version from the cache, or the draft for a preview.
/// </summary>
public sealed class SurveyRuntimeVersions
{
    /// <summary>
    /// The version id of preview responses that run the draft.
    /// </summary>
    public const string DraftVersionId = "draft";

    private readonly ISurveyVersionCache _versions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyRuntimeVersions"/> class.
    /// </summary>
    /// <param name="versions">The version cache.</param>
    public SurveyRuntimeVersions(ISurveyVersionCache versions)
    {
        _versions = versions;
    }

    /// <summary>
    /// Builds the in-memory version of a survey's draft. It is never saved.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns>The version.</returns>
    public static SurveyVersion Draft(Survey survey)
        => new()
        {
            ItemId = DraftVersionId,
            SurveyId = survey.ItemId,
            Number = 0,
            Definition = survey.Draft ?? new SurveyDefinition(),
        };

    /// <summary>
    /// Gets the version a response runs on.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version, or <see langword="null"/>.</returns>
    public Task<SurveyVersion> GetAsync(Survey survey, SurveyResponse response, CancellationToken cancellationToken = default)
        => string.Equals(response?.SurveyVersionId, DraftVersionId, StringComparison.Ordinal)
            ? Task.FromResult(survey is null ? null : Draft(survey))
            : _versions.GetAsync(response?.SurveyVersionId, cancellationToken);

    /// <summary>
    /// Gets the cache key of a version's compiled formulas: none for the draft, which changes.
    /// </summary>
    /// <param name="version">The version.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    public static string CacheKey(SurveyVersion version)
        => version is null || string.Equals(version.ItemId, DraftVersionId, StringComparison.Ordinal) ? null : version.ItemId;
}
