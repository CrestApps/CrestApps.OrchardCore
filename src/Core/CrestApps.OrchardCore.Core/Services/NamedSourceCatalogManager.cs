using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Core.Services;

public class NamedSourceCatalogManager<T> : SourceCatalogManager<T>, INamedCatalogManager<T>, ISourceCatalogManager<T>, INamedSourceCatalogManager<T>
    where T : CatalogEntry, INameAwareModel, ISourceAwareModel, new()
{
    protected readonly INamedSourceCatalog<T> NamedSourceModelStore;

    public NamedSourceCatalogManager(
        INamedSourceCatalog<T> store,
        IEnumerable<ICatalogEntryHandler<T>> handlers,
        ILogger<CatalogManager<T>> logger)
        : base(store, handlers, logger)
    {
        NamedSourceModelStore = store;
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        var model = await NamedSourceModelStore.FindByNameAsync(name);

        if (model is not null)
        {
            await LoadAsync(model);
        }

        return model;
    }

    public async ValueTask<T> GetAsync(string name, string source)
    {
        var model = await NamedSourceModelStore.GetAsync(name, source);

        if (model is not null)
        {
            await LoadAsync(model);
        }

        return model;
    }
}
