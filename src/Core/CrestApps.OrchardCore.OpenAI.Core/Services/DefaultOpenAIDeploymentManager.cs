using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class DefaultOpenAIDeploymentManager : IOpenAIDeploymentManager
{
    private readonly IOpenAIDeploymentStore _deploymentStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IOpenAIDeploymentHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultOpenAIDeploymentManager(
        IOpenAIDeploymentStore deploymentStore,
        IServiceProvider serviceProvider,
        IEnumerable<IOpenAIDeploymentHandler> handlers,
        ILogger<DefaultOpenAIDeploymentManager> logger)
    {
        _deploymentStore = deploymentStore;
        _serviceProvider = serviceProvider;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task<bool> DeleteAsync(OpenAIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var deletingContext = new DeletingOpenAIDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, _logger);

        if (string.IsNullOrEmpty(deployment.Id))
        {
            return false;
        }

        var removed = await _deploymentStore.DeleteAsync(deployment);

        var deletedContext = new DeletedOpenAIDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, _logger);

        return removed;
    }

    public async Task<OpenAIDeployment> FindByIdAsync(string id)
    {
        var deployment = await _deploymentStore.FindByIdAsync(id);

        if (deployment is not null)
        {
            await LoadAsync(deployment);

            return deployment;
        }

        return null;
    }

    public async Task<OpenAIDeployment> NewAsync(string source, JsonNode data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var deploymentSource = _serviceProvider.GetKeyedService<IOpenAIDeploymentSource>(source);

        if (deploymentSource == null)
        {
            _logger.LogWarning("Unable to find a deployment-source that can handle the source '{Source}'.", source);

            return null;
        }

        var id = IdGenerator.GenerateId();

        var deployment = new OpenAIDeployment()
        {
            Id = id,
            Source = source,
        };

        var initializingContext = new InitializingOpenAIDeploymentContext(deployment, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), initializingContext, _logger);

        var initializedContext = new InitializedOpenAIDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), initializedContext, _logger);

        // Set the source and the connectionName again after calling handlers to prevent handlers from updating the source during initialization.
        deployment.Source = source;

        if (string.IsNullOrEmpty(deployment.Id))
        {
            deployment.Id = id;
        }

        return deployment;
    }

    public async Task<OpenAIDeploymentResult> PageQueriesAsync(int page, int pageSize, QueryContext context)
    {
        var result = await _deploymentStore.PageAsync(page, pageSize, context);

        foreach (var record in result.Deployments)
        {
            await LoadAsync(record);
        }

        return result;
    }

    public async Task<IEnumerable<OpenAIDeployment>> GetAllAsync()
    {
        var deployments = await _deploymentStore.GetAllAsync();

        foreach (var deployment in deployments)
        {
            await LoadAsync(deployment);
        }

        return deployments;
    }

    public async Task SaveAsync(OpenAIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var savingContext = new SavingModelDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavingAsync(ctx), savingContext, _logger);

        await _deploymentStore.SaveAsync(deployment);

        var savedContext = new SavedOpenAIDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavedAsync(ctx), savedContext, _logger);
    }

    public async Task UpdateAsync(OpenAIDeployment deployment, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var updatingContext = new UpdatingModelDeploymentContext(deployment, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updatingContext, _logger);

        var updatedContext = new UpdatedModelDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updatedContext, _logger);
    }

    public async Task<OpenAIDeploymentValidateResult> ValidateAsync(OpenAIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var validatingContext = new ValidatingOpenAIDeploymentContext(deployment);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatingAsync(ctx), validatingContext, _logger);

        var validatedContext = new ValidatedModelDeploymentContext(deployment, validatingContext.Result);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatedAsync(ctx), validatedContext, _logger);

        return validatingContext.Result;
    }

    private Task LoadAsync(OpenAIDeployment deployment)
    {
        var loadedContext = new LoadedOpenAIDeploymentContext(deployment);

        return _handlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, _logger);
    }
}
