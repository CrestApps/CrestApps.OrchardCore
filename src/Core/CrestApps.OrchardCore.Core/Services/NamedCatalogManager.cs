using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Core.Services;

public class NamedCatalogManager<T> : CatalogManager<T>, INamedCatalogManager<T>
    where T : CatalogEntry, INameAwareModel, new()
{
    protected readonly INamedCatalog<T> NamedModelStore;

    public NamedCatalogManager(
        INamedCatalog<T> store,
        IEnumerable<ICatalogEntryHandler<T>> handlers,
        ILogger<CatalogManager<T>> logger)
        : base(store, handlers, logger)
    {
        NamedModelStore = store;
    }

    protected NamedCatalogManager(
        INamedCatalog<T> store,
        IEnumerable<ICatalogEntryHandler<T>> handlers,
        ILogger logger)
    : base(store, handlers, logger)
    {
        NamedModelStore = store;
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        var model = await NamedModelStore.FindByNameAsync(name);

        if (model is not null)
        {
            await LoadAsync(model);
        }

        return model;
    }
}
