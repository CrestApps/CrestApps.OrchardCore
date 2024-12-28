using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class DefaultModelDeploymentManager : IModelDeploymentManager
{
    private readonly IModelDeploymentStore _deploymentStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IModelDeploymentHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultModelDeploymentManager(
        IModelDeploymentStore deploymentStore,
        IServiceProvider serviceProvider,
        IEnumerable<IModelDeploymentHandler> handlers,
        ILogger<DefaultModelDeploymentManager> logger)
    {
        _deploymentStore = deploymentStore;
        _serviceProvider = serviceProvider;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task<bool> DeleteAsync(ModelDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var deletingContext = new DeletingModelDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, _logger);

        if (string.IsNullOrEmpty(deployment.Id))
        {
            return false;
        }

        var removed = await _deploymentStore.DeleteAsync(deployment);

        var deletedContext = new DeletedModelDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, _logger);

        return removed;
    }

    public async Task<ModelDeployment> FindByIdAsync(string id)
    {
        var deployment = await _deploymentStore.FindByIdAsync(id);

        if (deployment is not null)
        {
            await LoadAsync(deployment);

            return deployment;
        }

        return null;
    }

    public async Task<ModelDeployment> NewAsync(string source, JsonNode data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var deploymentSource = _serviceProvider.GetKeyedService<IModelDeploymentSource>(source);

        if (deploymentSource == null)
        {
            _logger.LogWarning("Unable to find a deployment-source that can handle the source '{Source}'.", source);

            return null;
        }

        var id = IdGenerator.GenerateId();

        var deployment = new ModelDeployment()
        {
            Id = id,
            Source = source,
        };

        var initializingContext = new InitializingModelDeploymentContext(deployment, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), initializingContext, _logger);

        var initializedContext = new InitializedModelDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), initializedContext, _logger);

        // Set the source and the connectionName again after calling handlers to prevent handlers from updating the source during initialization.
        deployment.Source = source;

        if (string.IsNullOrEmpty(deployment.Id))
        {
            deployment.Id = id;
        }

        return deployment;
    }

    public async Task<ModelDeploymentResult> PageQueriesAsync(int page, int pageSize, QueryContext context)
    {
        var result = await _deploymentStore.PageAsync(page, pageSize, context);

        foreach (var record in result.Deployments)
        {
            await LoadAsync(record);
        }

        return result;
    }

    public async Task<IEnumerable<ModelDeployment>> GetAllAsync()
    {
        var deployments = await _deploymentStore.GetAllAsync();

        foreach (var deployment in deployments)
        {
            await LoadAsync(deployment);
        }

        return deployments;
    }

    public async Task SaveAsync(ModelDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var savingContext = new SavingModelDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavingAsync(ctx), savingContext, _logger);

        await _deploymentStore.SaveAsync(deployment);

        var savedContext = new SavedModelDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavedAsync(ctx), savedContext, _logger);
    }

    public async Task UpdateAsync(ModelDeployment deployment, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var updatingContext = new UpdatingModelDeploymentContext(deployment, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updatingContext, _logger);

        var updatedContext = new UpdatedModelDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updatedContext, _logger);
    }

    public async Task<ModelDeploymentValidateResult> ValidateAsync(ModelDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var validatingContext = new ValidatingModelDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatingAsync(ctx), validatingContext, _logger);

        var validatedContext = new ValidatedModelDeploymentContext(deployment, validatingContext.Result);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatedAsync(ctx), validatedContext, _logger);

        return validatingContext.Result;
    }

    private Task LoadAsync(ModelDeployment deployment)
    {
        var loadedContext = new LoadedModelDeploymentContext(deployment);

        return _handlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, _logger);
    }
}
