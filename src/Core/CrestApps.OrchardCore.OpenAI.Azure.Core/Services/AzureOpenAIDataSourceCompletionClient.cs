using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIDataSourceCompletionClient : NamedAICompletionClient, IAICompletionClient
{
    public AzureOpenAIDataSourceCompletionClient(
               IAIClientFactory aIClientFactory,
               ILoggerFactory loggerFactory,
               IDistributedCache distributedCache,
               IOptions<AIProviderOptions> providerOptions,
               IEnumerable<IAICompletionServiceHandler> handlers,
               IOptions<DefaultAIOptions> defaultOptions
               ) : base(
                   AzureOpenAIConstants.AzureOpenAIOwnData,
                   aIClientFactory, distributedCache,
                   loggerFactory,
                   providerOptions.Value,
                   defaultOptions.Value,
                   handlers)
    {
    }

    protected override string ProviderName
        => AzureOpenAIConstants.ProviderName;
}
