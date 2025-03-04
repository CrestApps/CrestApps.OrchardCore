using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Core.Services;

public class NamedModelManager<T> : ModelManager<T>, INamedModelManager<T>
    where T : SourceModel, INameAwareModel, new()
{
    protected readonly INamedModelStore<T> NamedStore;

    public NamedModelManager(
        INamedModelStore<T> store,
        IEnumerable<IModelHandler<T>> handlers,
        ILogger<ModelManager<T>> logger)
        : base(store, handlers, logger)
    {
        NamedStore = store;
    }

    protected NamedModelManager(
        INamedModelStore<T> store,
        IEnumerable<IModelHandler<T>> handlers,
        ILogger logger)
    : base(store, handlers, logger)
    {
        NamedStore = store;
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        var model = await NamedStore.FindByNameAsync(name);

        if (model is not null)
        {
            await LoadAsync(model);
        }

        return model;
    }

    public async ValueTask<T> GetAsync(string name, string source)
    {
        var model = await NamedStore.GetAsync(name, source);

        if (model is not null)
        {
            await LoadAsync(model);
        }

        return model;
    }
}
