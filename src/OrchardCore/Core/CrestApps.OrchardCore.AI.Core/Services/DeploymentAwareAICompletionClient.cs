using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.Core.Templates.Services;
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
        ITemplateService aiTemplateService,
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
