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

    public OrchardCoreSearchIndexProfileStore(IIndexProfileStore store)
    {
        _store = store;
    }

    public async ValueTask<SearchIndexProfile> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => Map(await _store.FindByNameAsync(name));

    public async Task<IReadOnlyCollection<SearchIndexProfile>> GetByTypeAsync(string type)
        => (await _store.GetByTypeAsync(type)).Select(Map).ToArray();

    public async ValueTask<SearchIndexProfile> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => Map(await _store.FindByIdAsync(id));

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

    public async ValueTask<IReadOnlyCollection<SearchIndexProfile>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _store.GetAllAsync()).Select(Map).ToArray();

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
