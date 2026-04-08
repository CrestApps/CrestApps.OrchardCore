using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
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

    public async Task<SearchIndexProfile> FindByNameAsync(string name)
        => Map(await _store.FindByNameAsync(name));

    public async Task<IReadOnlyCollection<SearchIndexProfile>> GetByTypeAsync(string type)
        => (await _store.GetByTypeAsync(type)).Select(Map).ToArray();

    public async ValueTask<SearchIndexProfile> FindByIdAsync(string id)
        => Map(await _store.FindByIdAsync(id));

    public async ValueTask<IReadOnlyCollection<SearchIndexProfile>> GetAsync(IEnumerable<string> ids)
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

    public async ValueTask<IReadOnlyCollection<SearchIndexProfile>> GetAllAsync()
        => (await _store.GetAllAsync()).Select(Map).ToArray();

    public async ValueTask<global::CrestApps.Core.Models.PageResult<SearchIndexProfile>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : global::CrestApps.Core.Models.QueryContext
    {
        var entries = await GetAllAsync();
        var skip = (page - 1) * pageSize;

        return new global::CrestApps.Core.Models.PageResult<SearchIndexProfile>
        {
            Count = entries.Count,
            Entries = entries.Skip(skip).Take(pageSize).ToArray(),
        };
    }

    public async ValueTask CreateAsync(SearchIndexProfile record)
        => await _store.CreateAsync(Map(record));

    public async ValueTask UpdateAsync(SearchIndexProfile record)
        => await _store.UpdateAsync(Map(record));

    public async ValueTask<bool> DeleteAsync(SearchIndexProfile entry)
        => await _store.DeleteAsync(Map(entry));

    public async ValueTask SaveChangesAsync()
        => await _store.SaveChangesAsync();

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
            return new JsonObject();
        }

        return JsonSerializer.SerializeToNode(properties) as JsonObject ?? new JsonObject();
    }
}
