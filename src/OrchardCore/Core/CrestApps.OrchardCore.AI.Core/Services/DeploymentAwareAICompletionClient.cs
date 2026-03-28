using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.AI.Prompting.Services;
using CrestApps.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class DeploymentAwareAICompletionClient : NamedAICompletionClient
{
    private readonly ICatalog<AIDeployment> _store;
    private readonly IAIDeploymentManager _deploymentManager;

    public DeploymentAwareAICompletionClient(
        string name,
        IAIClientFactory aIClientFactory,
        IDistributedCache distributedCache,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        AIProviderOptions providerOptions,
        DefaultAIOptions defaultOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        ICatalog<AIDeployment> deploymentStore,
        IAITemplateService aiTemplateService,
        IAIDeploymentManager deploymentManager)
        : base(
            name,
            aIClientFactory,
            distributedCache,
            loggerFactory,
            serviceProvider,
            providerOptions,
            defaultOptions,
            handlers,
            aiTemplateService,
            deploymentManager)
    {
        _store = deploymentStore;
        _deploymentManager = deploymentManager;
    }

    protected override async Task<AIDeployment> GetDeploymentAsync(AICompletionContext content)
    {
        if (!string.IsNullOrEmpty(content.ChatDeploymentId))
        {
            if (_deploymentManager != null)
            {
                var deployment = await _deploymentManager.FindByIdAsync(content.ChatDeploymentId);

                if (deployment != null)
                {
                    return deployment;
                }
            }

            return await _store.FindByIdAsync(content.ChatDeploymentId);
        }

        return null;
    }
}
