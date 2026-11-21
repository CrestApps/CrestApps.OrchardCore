using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A version cache that reads the store each time, for results tests.
/// </summary>
internal sealed class ResultsVersionCache : ISurveyVersionCache
{
    private readonly SurveyVersionStore _store;

    public ResultsVersionCache(SurveyVersionStore store)
    {
        _store = store;
    }

    public async Task<SurveyVersion> GetAsync(string versionId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(versionId) ? null : await _store.FindByIdAsync(versionId, cancellationToken);

    public Task<SurveyVersion> GetPublishedAsync(Survey survey, CancellationToken cancellationToken = default)
        => GetAsync(survey?.PublishedVersionId, cancellationToken);

    public Task<IReadOnlyList<SurveyVersion>> ListAsync(string surveyId, CancellationToken cancellationToken = default)
        => _store.ListBySurveyAsync(surveyId, cancellationToken);
}
