using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Models;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

internal sealed class OrchardCoreSearchIndexProfileStore : ISearchIndexProfileStore
{
    private readonly IIndexProfileStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreSearchIndexProfileStore"/> class.
    /// </summary>
    /// <param name="store">The underlying Orchard Core index profile store.</param>
    public OrchardCoreSearchIndexProfileStore(IIndexProfileStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Finds a search index profile by its unique name.
    /// </summary>
    /// <param name="name">The name of the index profile.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The matching <see cref="SearchIndexProfile"/>, or <see langword="null"/> if not found.</returns>
    public async ValueTask<SearchIndexProfile> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => Map(await _store.FindByNameAsync(name));

    /// <summary>
    /// Retrieves all search index profiles of the specified type.
    /// </summary>
    /// <param name="type">The index profile type to filter by.</param>
    /// <returns>A read-only collection of matching <see cref="SearchIndexProfile"/> entries.</returns>
    public async Task<IReadOnlyCollection<SearchIndexProfile>> GetByTypeAsync(string type)
        => (await _store.GetByTypeAsync(type)).Select(Map).ToArray();

    /// <summary>
    /// Finds a search index profile by its unique identifier.
    /// </summary>
    /// <param name="id">The identifier of the index profile.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The matching <see cref="SearchIndexProfile"/>, or <see langword="null"/> if not found.</returns>
    public async ValueTask<SearchIndexProfile> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => Map(await _store.FindByIdAsync(id));

    /// <summary>
    /// Retrieves search index profiles matching the specified identifiers.
    /// </summary>
    /// <param name="ids">The identifiers of the index profiles to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only collection of matching <see cref="SearchIndexProfile"/> entries.</returns>
    public async ValueTask<IReadOnlyCollection<SearchIndexProfile>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);

        if (idSet.Count == 0)
        {
            return [];
        }

        return (await _store.GetAllAsync())
            .Where(profile => idSet.Contains(profile.Id))
            .Select(Map)
            .ToArray();
    }

    /// <summary>
    /// Retrieves all search index profiles.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only collection of all <see cref="SearchIndexProfile"/> entries.</returns>
    public async ValueTask<IReadOnlyCollection<SearchIndexProfile>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _store.GetAllAsync()).Select(Map).ToArray();

    /// <summary>
    /// Returns a paginated list of search index profiles.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="context">The query context containing optional filters.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <typeparam name="TQuery">The query context type.</typeparam>
    /// <returns>A <see cref="PageResult{SearchIndexProfile}"/> containing the total count and the requested page.</returns>
    public async ValueTask<PageResult<SearchIndexProfile>> PageAsync<TQuery>(
        int page,
        int pageSize,
        TQuery context,
        CancellationToken cancellationToken = default)
        where TQuery : CrestApps.Core.Models.QueryContext
    {
        var entries = await GetAllAsync(cancellationToken);
        var skip = (page - 1) * pageSize;

        return new PageResult<SearchIndexProfile>
        {
            Count = entries.Count,
            Entries = entries.Skip(skip).Take(pageSize).ToArray(),
        };
    }

    public async ValueTask CreateAsync(SearchIndexProfile record, CancellationToken cancellationToken = default)
        => await _store.CreateAsync(Map(record));

    public async ValueTask UpdateAsync(SearchIndexProfile record, CancellationToken cancellationToken = default)
        => await _store.UpdateAsync(Map(record));

    /// <summary>
    /// Deletes the specified search index profile.
    /// </summary>
    /// <param name="entry">The search index profile to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the profile was deleted; otherwise <see langword="false"/>.</returns>
    public async ValueTask<bool> DeleteAsync(SearchIndexProfile entry, CancellationToken cancellationToken = default)
        => await _store.DeleteAsync(Map(entry));

    private static SearchIndexProfile Map(IndexProfile profile)
    {
        if (profile == null)
        {
            return null;
        }

        return new SearchIndexProfile
        {
            ItemId = profile.Id,
            Name = profile.Name,
            IndexName = profile.IndexName,
            ProviderName = profile.ProviderName,
            IndexFullName = profile.IndexFullName,
            Type = profile.Type,
            Properties = DeserializeProperties(profile.Properties),
        };
    }

    private static IndexProfile Map(SearchIndexProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new IndexProfile
        {
            Id = profile.ItemId,
            Name = profile.Name,
            IndexName = profile.IndexName,
            ProviderName = profile.ProviderName,
            IndexFullName = profile.IndexFullName,
            Type = profile.Type,
            Properties = SerializeProperties(profile.Properties),
        };
    }

    private static Dictionary<string, object> DeserializeProperties(JsonObject properties)
    {
        if (properties == null)
        {
            return [];
        }

        return properties.Deserialize<Dictionary<string, object>>() ?? [];
    }

    private static JsonObject SerializeProperties(IDictionary<string, object> properties)
    {
        if (properties == null)
        {
            return [];
        }

        return JsonSerializer.SerializeToNode(properties) as JsonObject ?? new JsonObject();
    }
}
