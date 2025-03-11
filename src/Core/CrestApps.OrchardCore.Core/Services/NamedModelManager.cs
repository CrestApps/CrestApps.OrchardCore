using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Core.Services;

public class NamedModelManager<T> : ModelManager<T>, INamedModelManager<T>
    where T : Model, INameAwareModel, new()
{
    protected readonly INamedModelStore<T> NamedModelStore;

    public NamedModelManager(
        INamedModelStore<T> store,
        IEnumerable<IModelHandler<T>> handlers,
        ILogger<ModelManager<T>> logger)
        : base(store, handlers, logger)
    {
        NamedModelStore = store;
    }

    protected NamedModelManager(
        INamedModelStore<T> store,
        IEnumerable<IModelHandler<T>> handlers,
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
