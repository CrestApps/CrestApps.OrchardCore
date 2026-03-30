using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class DeploymentAwareAICompletionClient : NamedAICompletionClient
{
    private readonly INamedCatalog<AIDeployment> _store;
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
        INamedCatalog<AIDeployment> deploymentStore,
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
        if (!string.IsNullOrEmpty(content.ChatDeploymentName))
        {
            if (_deploymentManager != null)
            {
                var deployment = await _deploymentManager.FindByNameAsync(content.ChatDeploymentName);

                if (deployment != null)
                {
                    return deployment;
                }
            }

            return await _store.FindByNameAsync(content.ChatDeploymentName);
        }

        return null;
    }
}
