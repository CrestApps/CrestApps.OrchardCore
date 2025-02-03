using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDeploymentManager : IAIDeploymentManager
{
    private readonly IAIDeploymentStore _deploymentStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IAIDeploymentHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultAIDeploymentManager(
        IAIDeploymentStore deploymentStore,
        IServiceProvider serviceProvider,
        IEnumerable<IAIDeploymentHandler> handlers,
        ILogger<DefaultAIDeploymentManager> logger)
    {
        _deploymentStore = deploymentStore;
        _serviceProvider = serviceProvider;
        _handlers = handlers;
        _logger = logger;
    }

    public async ValueTask<bool> DeleteAsync(AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var deletingContext = new DeletingAIDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, _logger);

        if (string.IsNullOrEmpty(deployment.Id))
        {
            return false;
        }

        var removed = await _deploymentStore.DeleteAsync(deployment);

        var deletedContext = new DeletedAIDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, _logger);

        return removed;
    }

    public async ValueTask<AIDeployment> FindByIdAsync(string id)
    {
        var deployment = await _deploymentStore.FindByIdAsync(id);

        if (deployment is not null)
        {
            await LoadAsync(deployment);

            return deployment;
        }

        return null;
    }

    public async ValueTask<AIDeployment> NewAsync(string source, JsonNode data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var deploymentSource = _serviceProvider.GetKeyedService<IAIDeploymentProvider>(source);

        if (deploymentSource == null)
        {
            _logger.LogWarning("Unable to find a deployment-source that can handle the source '{Source}'.", source);

            return null;
        }

        var id = IdGenerator.GenerateId();

        var deployment = new AIDeployment()
        {
            Id = id,
            ProviderName = source,
        };

        var initializingContext = new InitializingAIDeploymentContext(deployment, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), initializingContext, _logger);

        var initializedContext = new InitializedAIDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), initializedContext, _logger);

        // Set the source and the connectionName again after calling handlers to prevent handlers from updating the source during initialization.
        deployment.ProviderName = source;

        if (string.IsNullOrEmpty(deployment.Id))
        {
            deployment.Id = id;
        }

        return deployment;
    }

    public async ValueTask<AIDeploymentResult> PageQueriesAsync(int page, int pageSize, QueryContext context)
    {
        var result = await _deploymentStore.PageAsync(page, pageSize, context);

        foreach (var record in result.Deployments)
        {
            await LoadAsync(record);
        }

        return result;
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetAllAsync()
    {
        var deployments = await _deploymentStore.GetAllAsync();

        foreach (var deployment in deployments)
        {
            await LoadAsync(deployment);
        }

        return deployments;
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetAsync(string providerName)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        var deployments = (await _deploymentStore.GetAllAsync()).Where(deployment => deployment.ProviderName == providerName);

        foreach (var deployment in deployments)
        {
            await LoadAsync(deployment);
        }

        return deployments;
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetAsync(string providerName, string connectionName)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        var deployments = (await _deploymentStore.GetAllAsync()).Where(deployment => deployment.ProviderName == providerName && deployment.ConnectionName == connectionName);

        foreach (var deployment in deployments)
        {
            await LoadAsync(deployment);
        }

        return deployments;
    }

    public async ValueTask SaveAsync(AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var savingContext = new SavingModelDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavingAsync(ctx), savingContext, _logger);

        await _deploymentStore.SaveAsync(deployment);

        var savedContext = new SavedAIDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavedAsync(ctx), savedContext, _logger);
    }

    public async ValueTask UpdateAsync(AIDeployment deployment, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var updatingContext = new UpdatingModelDeploymentContext(deployment, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updatingContext, _logger);

        var updatedContext = new UpdatedModelDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updatedContext, _logger);
    }

    public async ValueTask<AIDeploymentValidateResult> ValidateAsync(AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var validatingContext = new ValidatingAIDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatingAsync(ctx), validatingContext, _logger);

        var validatedContext = new ValidatedModelDeploymentContext(deployment, validatingContext.Result);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatedAsync(ctx), validatedContext, _logger);

        return validatingContext.Result;
    }

    private Task LoadAsync(AIDeployment deployment)
    {
        var loadedContext = new LoadedAIDeploymentContext(deployment);

        return _handlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, _logger);
    }
}
