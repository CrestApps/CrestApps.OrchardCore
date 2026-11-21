using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Caches immutable survey versions in memory by tenant and id. A version never changes, so entries need no
/// invalidation; they slide out when unused.
/// </summary>
public sealed class SurveyVersionCache : ISurveyVersionCache
{
    private static readonly TimeSpan _slidingExpiration = TimeSpan.FromMinutes(30);

    private readonly SurveyVersionStore _store;
    private readonly IMemoryCache _memoryCache;
    private readonly string _tenant;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyVersionCache"/> class.
    /// </summary>
    /// <param name="store">The version store.</param>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="shellSettings">The tenant settings.</param>
    public SurveyVersionCache(
        SurveyVersionStore store,
        IMemoryCache memoryCache,
        ShellSettings shellSettings)
    {
        _store = store;
        _memoryCache = memoryCache;
        _tenant = shellSettings.Name;
    }

    /// <inheritdoc />
    public async Task<SurveyVersion> GetAsync(string versionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(versionId))
        {
            return null;
        }

        var key = CacheKey(versionId);

        if (_memoryCache.TryGetValue(key, out SurveyVersion cached))
        {
            return cached;
        }

        var version = await _store.FindByIdAsync(versionId, cancellationToken);

        if (version is not null)
        {
            _memoryCache.Set(key, version, new MemoryCacheEntryOptions { SlidingExpiration = _slidingExpiration });
        }

        return version;
    }

    /// <inheritdoc />
    public Task<SurveyVersion> GetPublishedAsync(Survey survey, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(survey?.PublishedVersionId)
            ? Task.FromResult<SurveyVersion>(null)
            : GetAsync(survey.PublishedVersionId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<SurveyVersion>> ListAsync(string surveyId, CancellationToken cancellationToken = default)
        => _store.ListBySurveyAsync(surveyId, cancellationToken);

    private string CacheKey(string versionId)
        => $"Surveys:Version:{_tenant}:{versionId}";
}
