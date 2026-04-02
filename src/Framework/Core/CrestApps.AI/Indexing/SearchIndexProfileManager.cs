using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using CrestApps.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.AI.Indexing;

public sealed class SearchIndexProfileManager : CatalogManager<SearchIndexProfile>, ISearchIndexProfileManager
{
    private readonly ISearchIndexProfileStore _store;
    private readonly IEnumerable<IIndexProfileHandler> _handlers;

    public SearchIndexProfileManager(
        ISearchIndexProfileStore store,
        IEnumerable<IIndexProfileHandler> handlers,
        ILogger<SearchIndexProfileManager> logger)
        : base(store, handlers, logger)
    {
        _store = store;
        _handlers = handlers;
    }

    public Task<SearchIndexProfile> FindByNameAsync(string name)
        => _store.FindByNameAsync(name);

    public Task<IReadOnlyCollection<SearchIndexProfile>> GetByTypeAsync(string type)
        => _store.GetByTypeAsync(type);

    public async ValueTask<IReadOnlyCollection<SearchIndexField>> GetFieldsAsync(
        SearchIndexProfile profile,
        CancellationToken cancellationToken = default)
    {
        foreach (var handler in _handlers)
        {
            var fields = await handler.GetFieldsAsync(profile, cancellationToken);
            if (fields != null)
            {
                return fields;
            }
        }

        return null;
    }

    public async Task ResetAsync(SearchIndexProfile profile, CancellationToken cancellationToken = default)
    {
        foreach (var handler in _handlers)
        {
            await handler.ResetAsync(profile, cancellationToken);
        }
    }

    public async Task SynchronizeAsync(SearchIndexProfile profile, CancellationToken cancellationToken = default)
    {
        foreach (var handler in _handlers)
        {
            await handler.SynchronizedAsync(profile, cancellationToken);
        }
    }
}
