using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class OpenAICompletionClient : DeploymentAwareAICompletionClient
{
    public OpenAICompletionClient(
        IAIClientFactory aIClientFactory,
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        IOptions<DefaultAIOptions> defaultOptions,
        INamedCatalog<AIDeployment> deploymentStore
        ) : base(
            OpenAIConstants.ImplementationName,
            aIClientFactory,
            distributedCache,
            loggerFactory,
            providerOptions.Value,
            defaultOptions.Value,
            handlers,
            deploymentStore)
    {
    }

    protected override string ProviderName
        => OpenAIConstants.ProviderName;
}
