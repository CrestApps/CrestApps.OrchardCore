using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class DeploymentAwareAICompletionClient : NamedAICompletionClient
{
    private readonly IModelStore<AIDeployment> _store;

    public DeploymentAwareAICompletionClient(
        string name,
        IDistributedCache distributedCache,
        ILoggerFactory loggerFactory,
        AIProviderOptions providerOptions,
        DefaultAIOptions defaultOptions,
        IAIToolsService toolsService,
        IModelStore<AIDeployment> deploymentStore)
        : base(name, distributedCache, loggerFactory, providerOptions, defaultOptions, toolsService)
    {
        _store = deploymentStore;
    }

    protected override async Task<AIDeployment> GetDeploymentAsync(AICompletionContext content)
    {
        if (!string.IsNullOrEmpty(content.Profile?.DeploymentId))
        {
            return await _store.FindByIdAsync(content.Profile.DeploymentId);
        }

        return null;
    }
}
