using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Core.Services;

public class NamedSourceCatalogManager<T> : SourceCatalogManager<T>, INamedCatalogManager<T>, ISourceCatalogManager<T>, INamedSourceCatalogManager<T>
    where T : CatalogItem, INameAwareModel, ISourceAwareModel, new()
{
    protected readonly INamedSourceCatalog<T> NamedSourceModelStore;

    public NamedSourceCatalogManager(
        INamedSourceCatalog<T> store,
        IEnumerable<ICatalogEntryHandler<T>> handlers,
        ILogger<NamedSourceCatalogManager<T>> logger)
        : base(store, handlers, logger)
    {
        NamedSourceModelStore = store;
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        var entry = await NamedSourceModelStore.FindByNameAsync(name);

        if (entry is not null)
        {
            await LoadAsync(entry);
        }

        return entry;
    }

    public async ValueTask<T> GetAsync(string name, string source)
    {
        var entry = await NamedSourceModelStore.GetAsync(name, source);

        if (entry is not null)
        {
            await LoadAsync(entry);
        }

        return entry;
    }
}
