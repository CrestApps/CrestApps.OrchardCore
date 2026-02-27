using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class DeploymentAwareAICompletionClient : NamedAICompletionClient
{
    private readonly ICatalog<AIDeployment> _store;

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
        IAITemplateService aiTemplateService)
        : base(
            name,
            aIClientFactory,
            distributedCache,
            loggerFactory,
            serviceProvider,
            providerOptions,
            defaultOptions,
            handlers,
            aiTemplateService)
    {
        _store = deploymentStore;
    }

    protected override async Task<AIDeployment> GetDeploymentAsync(AICompletionContext content)
    {
        if (!string.IsNullOrEmpty(content.DeploymentId))
        {
            return await _store.FindByIdAsync(content.DeploymentId);
        }

        return null;
    }
}
