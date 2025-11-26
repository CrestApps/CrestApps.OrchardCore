using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAICompletionClient : OpenAICompletionClient
{
    public AzureOpenAICompletionClient(
        IAIClientFactory aIClientFactory,
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        IOptions<DefaultAIOptions> defaultOptions,
        INamedCatalog<AIDeployment> deploymentStore,
        IEnumerable<IOpenAIChatOptionsConfiguration> openAIChatOptionsConfigurations
        ) : base(
            AzureOpenAIConstants.StandardImplementationName,
            aIClientFactory,
            loggerFactory,
            distributedCache,
            providerOptions.Value,
            handlers,
            defaultOptions.Value,
            deploymentStore,
            openAIChatOptionsConfigurations)
    {
    }

    protected override string ProviderName
        => AzureOpenAIConstants.ProviderName;
}
