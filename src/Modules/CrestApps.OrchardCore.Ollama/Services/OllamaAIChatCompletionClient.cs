using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Ollama.Services;

internal sealed class OllamaAIChatCompletionClient : NamedAICompletionClient
{
    public OllamaAIChatCompletionClient(
           IAIClientFactory aIClientFactory,
           ILoggerFactory loggerFactory,
           IDistributedCache distributedCache,
           IServiceProvider serviceProvider,
           IOptions<AIProviderOptions> providerOptions,
           IEnumerable<IAICompletionServiceHandler> handlers,
           IOptions<DefaultAIOptions> defaultOptions
           ) : base(
               OllamaConstants.ImplementationName,
               aIClientFactory, distributedCache,
               loggerFactory,
               serviceProvider,
               providerOptions.Value,
               defaultOptions.Value,
               handlers)
    {
    }

    protected override string ProviderName
        => OllamaConstants.ProviderName;
}
