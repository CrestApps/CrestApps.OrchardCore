using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Core.DataSources;

/// <summary>
/// Wraps a provider's data source content manager so that every search passes its chunks through the registered
/// <see cref="IAIDataSourceResultFilter"/> instances. With no filter registered it is a pass-through.
/// </summary>
public sealed class FilteringDataSourceContentManager : IDataSourceContentManager
{
    private readonly IDataSourceContentManager _inner;
    private readonly IAIDataSourceResultFilter[] _filters;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AIDataSourceResultFilterOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilteringDataSourceContentManager"/> class.
    /// </summary>
    /// <param name="inner">The provider's content manager.</param>
    /// <param name="filters">The result filters.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, which gives the person of the request.</param>
    /// <param name="options">How many more chunks to ask the index for.</param>
    public FilteringDataSourceContentManager(
        IDataSourceContentManager inner,
        IEnumerable<IAIDataSourceResultFilter> filters,
        IHttpContextAccessor httpContextAccessor,
        AIDataSourceResultFilterOptions options)
    {
        _inner = inner;
        _filters = filters?.ToArray() ?? [];
        _httpContextAccessor = httpContextAccessor;
        _options = options ?? new AIDataSourceResultFilterOptions();
    }

    /// <summary>
    /// Gets the provider's content manager this one wraps.
    /// </summary>
    public IDataSourceContentManager Inner => _inner;

    /// <inheritdoc />
    public async Task<IEnumerable<DataSourceSearchResult>> SearchAsync(
        IIndexProfileInfo indexProfile,
        float[] embedding,
        string dataSourceId,
        int topN,
        string filter = null,
        CancellationToken cancellationToken = default)
    {
        if (_filters.Length == 0)
        {
            return await _inner.SearchAsync(indexProfile, embedding, dataSourceId, topN, filter, cancellationToken);
        }

        var results = await _inner.SearchAsync(indexProfile, embedding, dataSourceId, _options.GetCandidateCount(topN), filter, cancellationToken);
        var kept = await ApplyAsync(CreateContext(indexProfile, dataSourceId, cancellationToken), results);

        return kept.Take(topN).ToList();
    }

    /// <inheritdoc />
    public async Task<DataSourceSearchOutcome> TrySearchAsync(
        IIndexProfileInfo indexProfile,
        float[] embedding,
        string dataSourceId,
        int topN,
        string filter = null,
        CancellationToken cancellationToken = default)
    {
        if (_filters.Length == 0)
        {
            return await _inner.TrySearchAsync(indexProfile, embedding, dataSourceId, topN, filter, cancellationToken);
        }

        var outcome = await _inner.TrySearchAsync(indexProfile, embedding, dataSourceId, _options.GetCandidateCount(topN), filter, cancellationToken);

        // A provider that cannot tell a failed search from an empty one answers null, and the caller then searches
        // through SearchAsync, which filters too. A failed search has nothing to filter.
        if (outcome is null || !outcome.Succeeded)
        {
            return outcome;
        }

        var kept = await ApplyAsync(CreateContext(indexProfile, dataSourceId, cancellationToken), outcome.Results);

        return DataSourceSearchOutcome.Success(kept.Take(topN).ToList());
    }

    /// <inheritdoc />
    public Task<long> DeleteByDataSourceIdAsync(IIndexProfileInfo indexProfile, string dataSourceId, CancellationToken cancellationToken = default)
        => _inner.DeleteByDataSourceIdAsync(indexProfile, dataSourceId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> DeleteByReferenceIdsAsync(IIndexProfileInfo profile, string dataSourceId, IReadOnlyCollection<string> referenceIds, CancellationToken cancellationToken = default)
        => _inner.DeleteByReferenceIdsAsync(profile, dataSourceId, referenceIds, cancellationToken);

    private async Task<IReadOnlyList<DataSourceSearchResult>> ApplyAsync(AIDataSourceResultFilterContext context, IEnumerable<DataSourceSearchResult> results)
    {
        IReadOnlyList<DataSourceSearchResult> kept = results?.Where(result => result is not null).ToList() ?? [];

        foreach (var filter in _filters)
        {
            if (kept.Count == 0)
            {
                break;
            }

            kept = await filter.FilterAsync(context, kept) ?? [];
        }

        return kept;
    }

    internal AIDataSourceResultFilterContext CreateContext(IIndexProfileInfo indexProfile, string dataSourceId, CancellationToken cancellationToken)
    {
        var invocation = AIInvocationScope.Current;

        return new AIDataSourceResultFilterContext
        {
            IndexProfile = indexProfile,
            DataSourceId = dataSourceId,
            User = _httpContextAccessor?.HttpContext?.User,
            Resource = invocation?.ToolExecutionContext?.Resource ?? (object)invocation?.ChatInteraction,
            ProfileId = invocation?.ChatSession?.ProfileId ?? (invocation?.ToolExecutionContext?.Resource as AIProfile)?.ItemId,
            CancellationToken = cancellationToken,
        };
    }
}
