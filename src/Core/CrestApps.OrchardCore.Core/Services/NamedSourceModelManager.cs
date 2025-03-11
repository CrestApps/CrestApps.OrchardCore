using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Core.Services;

public class NamedSourceModelManager<T> : SourceModelManager<T>, INamedModelManager<T>, ISourceModelManager<T>, INamedSourceModelManager<T>
    where T : Model, INameAwareModel, ISourceAwareModel, new()
{
    protected readonly INamedSourceModelStore<T> NamedSourceModelStore;

    public NamedSourceModelManager(
        INamedSourceModelStore<T> store,
        IEnumerable<IModelHandler<T>> handlers,
        ILogger<ModelManager<T>> logger)
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
