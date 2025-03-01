using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Services;

public class ModelManager<T> : IModelManager<T>
    where T : SourceModel, new()
{
    protected readonly IModelStore<T> Store;
    protected readonly ILogger Logger;

    private readonly IEnumerable<IModelHandler<T>> _handlers;

    public ModelManager(
        IModelStore<T> store,
        IEnumerable<IModelHandler<T>> handlers,
        ILogger<ModelManager<T>> logger)
    {
        Store = store;
        _handlers = handlers;
        Logger = logger;
    }

    public async ValueTask<bool> DeleteAsync(T model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var deletingContext = new DeletingContext<T>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, Logger);

        if (string.IsNullOrEmpty(model.Id))
        {
            return false;
        }

        var removed = await Store.DeleteAsync(model);

        await DeletedAsync(model);

        var deletedContext = new DeletedContext<T>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, Logger);

        return removed;
    }

    public async ValueTask<T> FindByIdAsync(string id)
    {
        var model = await Store.FindByIdAsync(id);

        if (model is not null)
        {
            await LoadAsync(model);

            return model;
        }

        return null;
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
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), initializingContext, Logger);

        var initializedContext = new InitializedContext<T>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), initializedContext, Logger);

        // Set the source again after calling handlers to prevent handlers from updating the source during initialization.
        model.Source = source;

        if (string.IsNullOrEmpty(model.Id))
        {
            model.Id = id;
        }

        return model;
    }

    public async ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var result = await Store.PageAsync(page, pageSize, context);

        foreach (var model in result.Models)
        {
            await LoadAsync(model);
        }

        return result;
    }

    public async ValueTask SaveAsync(T model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var savingContext = new SavingContext<T>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavingAsync(ctx), savingContext, Logger);

        await Store.SaveAsync(model);

        var savedContext = new SavedContext<T>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavedAsync(ctx), savedContext, Logger);
    }

    public async ValueTask UpdateAsync(T model, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var updatingContext = new UpdatingContext<T>(model, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updatingContext, Logger);

        var updatedContext = new UpdatedContext<T>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updatedContext, Logger);
    }

    public async ValueTask<ValidationResultDetails> ValidateAsync(T model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var validatingContext = new ValidatingContext<T>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatingAsync(ctx), validatingContext, Logger);

        var validatedContext = new ValidatedContext<T>(model, validatingContext.Result);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatedAsync(ctx), validatedContext, Logger);

        return validatingContext.Result;
    }

    public async ValueTask<IEnumerable<T>> GetAllAsync()
    {
        var models = await Store.GetAllAsync();

        foreach (var model in models)
        {
            await LoadAsync(model);
        }

        return models;
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

    protected virtual ValueTask DeletedAsync(T model)
    {
        return ValueTask.CompletedTask;
    }

    protected virtual async Task LoadAsync(T model)
    {
        var loadedContext = new LoadedContext<T>(model);

        await _handlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, Logger);
    }
}
