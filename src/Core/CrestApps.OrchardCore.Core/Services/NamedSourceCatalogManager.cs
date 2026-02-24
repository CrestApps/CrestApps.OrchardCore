using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Core.Services;

public class NamedSourceCatalogManager<T> : SourceCatalogManager<T>, INamedCatalogManager<T>, ISourceCatalogManager<T>, INamedSourceCatalogManager<T>
    where T : CatalogItem, INameAwareModel, ISourceAwareModel, new()
{
    protected readonly INamedSourceCatalog<T> NamedSourceCatalog;

    public NamedSourceCatalogManager(
        INamedSourceCatalog<T> catalog,
        IEnumerable<ICatalogEntryHandler<T>> handlers,
        ILogger<NamedSourceCatalogManager<T>> logger)
        : base(catalog, handlers, logger)
    {
        NamedSourceCatalog = catalog;
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        var entry = await NamedSourceCatalog.FindByNameAsync(name);

        if (entry is not null)
        {
            await LoadAsync(entry);
        }

        return entry;
    }

    public async ValueTask<T> GetAsync(string name, string source)
    {
        var entry = await NamedSourceCatalog.GetAsync(name, source);

        if (entry is not null)
        {
            await LoadAsync(entry);
        }

        return entry;
    }
}
