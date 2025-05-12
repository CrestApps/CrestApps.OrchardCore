using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDataSourceManager : IAIDataSourceManager
{
    private readonly IAIDataSourceStore _store;
    private readonly IEnumerable<IModelHandler<AIDataSource>> _handlers;
    private readonly ILogger _logger;

    public DefaultAIDataSourceManager(
        IAIDataSourceStore store,
        IEnumerable<IModelHandler<AIDataSource>> handlers,
        ILogger<DefaultAIDataSourceManager> logger)
    {
        _store = store;
        _handlers = handlers;
        _logger = logger;
    }

    public async ValueTask<bool> DeleteAsync(AIDataSource model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var deletingContext = new DeletingContext<AIDataSource>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, _logger);

        if (string.IsNullOrEmpty(model.Id))
        {
            return false;
        }

        var removed = await _store.DeleteAsync(model);

        var deletedContext = new DeletedContext<AIDataSource>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, _logger);

        return removed;
    }

    public async ValueTask<AIDataSource> FindByIdAsync(string id)
    {
        var model = await _store.FindByIdAsync(id);

        if (model is not null)
        {
            await LoadAsync(model);

            return model;
        }

        return null;
    }

    public async ValueTask<AIDataSource> NewAsync(string providerName, string type, JsonNode data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(type);

        var id = IdGenerator.GenerateId();

        var model = new AIDataSource()
        {
            Id = id,
            ProfileSource = providerName,
            Type = type,
        };

        var initializingContext = new InitializingContext<AIDataSource>(model, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), initializingContext, _logger);

        var initializedContext = new InitializedContext<AIDataSource>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), initializedContext, _logger);

        if (string.IsNullOrEmpty(model.Id))
        {
            model.Id = id;
        }

        return model;
    }

    public async ValueTask<PageResult<AIDataSource>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var result = await _store.PageAsync(page, pageSize, context);

        foreach (var model in result.Models)
        {
            await LoadAsync(model);
        }

        return result;
    }

    public async ValueTask CreateAsync(AIDataSource model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var creatingContext = new CreatingContext<AIDataSource>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.CreatingAsync(ctx), creatingContext, _logger);

        await _store.CreateAsync(model);
        await _store.SaveChangesAsync();

        var createdContext = new CreatedContext<AIDataSource>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.CreatedAsync(ctx), createdContext, _logger);
    }

    public async ValueTask UpdateAsync(AIDataSource model, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var updatingContext = new UpdatingContext<AIDataSource>(model, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updatingContext, _logger);

        await _store.UpdateAsync(model);
        await _store.SaveChangesAsync();

        var updatedContext = new UpdatedContext<AIDataSource>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updatedContext, _logger);
    }

    public async ValueTask<ValidationResultDetails> ValidateAsync(AIDataSource model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var validatingContext = new ValidatingContext<AIDataSource>(model);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatingAsync(ctx), validatingContext, _logger);

        var validatedContext = new ValidatedContext<AIDataSource>(model, validatingContext.Result);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatedAsync(ctx), validatedContext, _logger);

        return validatingContext.Result;
    }

    public async ValueTask<IEnumerable<AIDataSource>> GetAllAsync()
    {
        var models = await _store.GetAllAsync();

        foreach (var model in models)
        {
            await LoadAsync(model);
        }

        return models;
    }

    public async ValueTask<IEnumerable<AIDataSource>> GetAsync(string providerName, string type)
    {
        var models = await _store.GetAsync(providerName, type);

        foreach (var model in models)
        {
            await LoadAsync(model);
        }

        return models;
    }

    private async Task LoadAsync(AIDataSource model)
    {
        var loadedContext = new LoadedContext<AIDataSource>(model);

        await _handlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, _logger);
    }
}
