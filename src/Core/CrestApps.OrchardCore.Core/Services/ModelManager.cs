using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Services;

public class ModelManager<T> : IModelManager<T>
    where T : Model, new()
{
    protected readonly IModelStore<T> Store;
    protected readonly ILogger Logger;
    protected readonly IEnumerable<IModelHandler<T>> Handlers;

    public ModelManager(
        IModelStore<T> store,
        IEnumerable<IModelHandler<T>> handlers,
        ILogger<ModelManager<T>> logger)
    {
        Store = store;
        Handlers = handlers;
        Logger = logger;
    }

    protected ModelManager(
        IModelStore<T> store,
        IEnumerable<IModelHandler<T>> handlers,
        ILogger logger)
    {
        Store = store;
        Handlers = handlers;
        Logger = logger;
    }

    public async ValueTask<bool> DeleteAsync(T model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var deletingContext = new DeletingContext<T>(model);
        await Handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, Logger);

        if (string.IsNullOrEmpty(model.Id))
        {
            return false;
        }

        var removed = await Store.DeleteAsync(model);

        await DeletedAsync(model);

        var deletedContext = new DeletedContext<T>(model);
        await Handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, Logger);

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

    public virtual async ValueTask<T> NewAsync(JsonNode data = null)
    {
        var id = IdGenerator.GenerateId();

        var model = new T()
        {
            Id = id,
        };

        var initializingContext = new InitializingContext<T>(model, data);
        await Handlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), initializingContext, Logger);

        var initializedContext = new InitializedContext<T>(model);
        await Handlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), initializedContext, Logger);

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

    public async ValueTask CreateAsync(T model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var creatingContext = new CreatingContext<T>(model);
        await Handlers.InvokeAsync((handler, ctx) => handler.CreatingAsync(ctx), creatingContext, Logger);

        await Store.CreateAsync(model);
        await Store.SaveChangesAsync();

        var createdContext = new CreatedContext<T>(model);
        await Handlers.InvokeAsync((handler, ctx) => handler.CreatedAsync(ctx), createdContext, Logger);
    }

    public async ValueTask UpdateAsync(T model, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var updatingContext = new UpdatingContext<T>(model, data);
        await Handlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updatingContext, Logger);

        await Store.UpdateAsync(model);
        await Store.SaveChangesAsync();

        var updatedContext = new UpdatedContext<T>(model);
        await Handlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updatedContext, Logger);
    }

    public async ValueTask<ValidationResultDetails> ValidateAsync(T model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var validatingContext = new ValidatingContext<T>(model);
        await Handlers.InvokeAsync((handler, ctx) => handler.ValidatingAsync(ctx), validatingContext, Logger);

        var validatedContext = new ValidatedContext<T>(model, validatingContext.Result);
        await Handlers.InvokeAsync((handler, ctx) => handler.ValidatedAsync(ctx), validatedContext, Logger);

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

    protected virtual ValueTask DeletedAsync(T model)
    {
        return ValueTask.CompletedTask;
    }

    protected virtual async Task LoadAsync(T model)
    {
        var loadedContext = new LoadedContext<T>(model);

        await Handlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, Logger);
    }
}
