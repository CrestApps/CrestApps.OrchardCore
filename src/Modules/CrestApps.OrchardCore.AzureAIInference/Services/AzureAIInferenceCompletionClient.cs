using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AzureAIInference.Services;

public sealed class AzureAIInferenceCompletionClient : DeploymentAwareAICompletionClient
{
    public AzureAIInferenceCompletionClient(
        IAIClientFactory aIClientFactory,
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        IServiceProvider serviceProvider,
        IOptions<DefaultAIOptions> defaultOptions,
        INamedCatalog<AIDeployment> deploymentStore
        ) : base(
            AzureAIInferenceConstants.ImplementationName,
            aIClientFactory,
            distributedCache,
            loggerFactory,
            serviceProvider,
            providerOptions.Value,
            defaultOptions.Value,
            handlers,
            deploymentStore)
    {
    }

    protected override string ProviderName
        => AzureAIInferenceConstants.ProviderName;
}
