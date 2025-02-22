using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIToolInstanceManager : IAIToolInstanceManager
{
    private readonly IAIToolInstanceStore _store;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IAIToolInstanceHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultAIToolInstanceManager(
        IAIToolInstanceStore store,
        IServiceProvider serviceProvider,
        IEnumerable<IAIToolInstanceHandler> handlers,
        ILogger<DefaultAIProfileManager> logger)
    {
        _store = store;
        _serviceProvider = serviceProvider;
        _handlers = handlers;
        _logger = logger;
    }

    public async ValueTask<bool> DeleteAsync(AIToolInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var deletingContext = new DeletingAIToolInstanceContext(instance);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, _logger);

        if (string.IsNullOrEmpty(instance.Id))
        {
            return false;
        }

        var removed = await _store.DeleteAsync(instance);

        var deletedContext = new DeletedAIToolInstanceContext(instance);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, _logger);

        return removed;
    }

    public async ValueTask<AIToolInstance> FindByIdAsync(string id)
    {
        var instance = await _store.FindByIdAsync(id);

        if (instance is not null)
        {
            await LoadAsync(instance);

            return instance;
        }

        return null;
    }

    public async ValueTask<AIToolInstance> NewAsync(string source, JsonNode data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var instanceSource = _serviceProvider.GetKeyedService<IAIToolSource>(source);

        if (instanceSource == null)
        {
            _logger.LogWarning("Unable to find a tool-source that can handle the source '{Source}'.", source);

            return null;
        }

        var id = IdGenerator.GenerateId();

        var instance = new AIToolInstance()
        {
            Id = id,
            Source = source,
        };

        var initializingContext = new InitializingAIToolInstanceContext(instance, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), initializingContext, _logger);

        var initializedContext = new InitializedAIToolInstanceContext(instance);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), initializedContext, _logger);

        // Set the source again after calling handlers to prevent handlers from updating the source during initialization.
        instance.Source = source;

        if (string.IsNullOrEmpty(instance.Id))
        {
            instance.Id = id;
        }

        return instance;
    }

    public async ValueTask<AIToolInstancesResult> PageAsync(int page, int pageSize, QueryContext context)
    {
        var result = await _store.PageAsync(page, pageSize, context);

        foreach (var instance in result.Instances)
        {
            await LoadAsync(instance);
        }

        return result;
    }

    public async ValueTask SaveAsync(AIToolInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var savingContext = new SavingAIToolInstanceContext(instance);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavingAsync(ctx), savingContext, _logger);

        await _store.SaveAsync(instance);

        var savedContext = new SavedAIToolInstanceContext(instance);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavedAsync(ctx), savedContext, _logger);
    }

    public async ValueTask UpdateAsync(AIToolInstance instance, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var updatingContext = new UpdatingAIToolInstanceContext(instance, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updatingContext, _logger);

        var updatedContext = new UpdatedAIToolInstanceContext(instance);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updatedContext, _logger);
    }

    public async ValueTask<AIValidateResult> ValidateAsync(AIToolInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var validatingContext = new ValidatingAIToolInstanceContext(instance);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatingAsync(ctx), validatingContext, _logger);

        var validatedContext = new ValidatedAIToolInstanceContext(instance, validatingContext.Result);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatedAsync(ctx), validatedContext, _logger);

        return validatingContext.Result;
    }

    private Task LoadAsync(AIToolInstance instance)
    {
        var loadedContext = new LoadedAIToolInstanceContext(instance);

        return _handlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, _logger);
    }
}
