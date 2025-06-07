using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Services;

public class SourceCatalogManager<T> : CatalogManager<T>, ISourceCatalogManager<T>
    where T : CatalogEntry, ISourceAwareModel, new()
{
    protected readonly ISourceCatalog<T> SourceModelStore;

    public SourceCatalogManager(
        ISourceCatalog<T> store,
        IEnumerable<ICatalogEntryHandler<T>> handlers,
        ILogger<CatalogManager<T>> logger)
        : base(store, handlers, logger)
    {
        SourceModelStore = store;
    }

    public async ValueTask<IEnumerable<T>> FindBySourceAsync(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var models = (await Store.GetAllAsync()).Where(x => x.Source == source);

        foreach (var model in models)
        {
            await LoadAsync(model);
        }

        return models;
    }

    public async ValueTask<IEnumerable<T>> GetAsync(string source)
    {
        var models = await SourceModelStore.GetAsync(source);

        foreach (var model in models)
        {
            await LoadAsync(model);
        }

        return models;
    }

    public async ValueTask<T> NewAsync(string source, JsonNode data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var id = IdGenerator.GenerateId();

        var model = new T()
        {
            Id = id,
            Source = source,
        };

        var initializingContext = new InitializingContext<T>(model, data);
        await Handlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), initializingContext, Logger);

        var initializedContext = new InitializedContext<T>(model);
        await Handlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), initializedContext, Logger);

        if (string.IsNullOrEmpty(model.Id))
        {
            model.Id = id;
        }

        model.Source = source;

        return model;
    }

    public override ValueTask<T> NewAsync(JsonNode data = null)
    {
        var source = data?["Source"]?.GetValue<string>();

        if (string.IsNullOrEmpty(source))
        {
            throw new InvalidOperationException("Data must contain a Source entry");
        }

        return NewAsync(source, data);
    }
}
