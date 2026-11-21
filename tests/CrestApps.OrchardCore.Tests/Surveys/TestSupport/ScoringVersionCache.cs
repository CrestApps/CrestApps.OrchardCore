using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A version cache over versions the test adds.
/// </summary>
internal sealed class ScoringVersionCache : ISurveyVersionCache
{
    public Dictionary<string, SurveyVersion> Versions { get; } = new(StringComparer.Ordinal);

    public Task<SurveyVersion> GetAsync(string versionId, CancellationToken cancellationToken = default)
        => Task.FromResult(versionId is not null ? Versions.GetValueOrDefault(versionId) : null);

    public Task<SurveyVersion> GetPublishedAsync(Survey survey, CancellationToken cancellationToken = default)
        => GetAsync(survey?.PublishedVersionId, cancellationToken);

    public Task<IReadOnlyList<SurveyVersion>> ListAsync(string surveyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SurveyVersion>>(Versions.Values.Where(version => version.SurveyId == surveyId).ToList());
}
