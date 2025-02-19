using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class DeploymentNamedAICompletionClient : NamedAICompletionClient
{
    private readonly IAIDeploymentStore _deploymentStore;

    public DeploymentNamedAICompletionClient(
        string name,
        IDistributedCache distributedCache,
        ILoggerFactory loggerFactory,
        AIProviderOptions providerOptions,
        DefaultAIOptions defaultOptions,
        IAIToolsService toolsService,
        IAIDeploymentStore deploymentStore)
        : base(name, distributedCache, loggerFactory, providerOptions, defaultOptions, toolsService)
    {
        _deploymentStore = deploymentStore;
    }

    protected override async Task<AIDeployment> GetDeploymentAsync(AICompletionContext content)
    {
        if (!string.IsNullOrEmpty(content.Profile?.DeploymentId))
        {
            return await _deploymentStore.FindByIdAsync(content.Profile.DeploymentId);
        }

        return null;
    }
}
