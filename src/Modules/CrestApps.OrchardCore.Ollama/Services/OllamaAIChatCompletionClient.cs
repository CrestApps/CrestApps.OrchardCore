using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Ollama.Services;

internal sealed class OllamaAIChatCompletionClient : NamedAICompletionClient
{
    public OllamaAIChatCompletionClient(
           ILoggerFactory loggerFactory,
           IDistributedCache distributedCache,
           IOptions<AIProviderOptions> providerOptions,
           IEnumerable<IAICompletionServiceHandler> handlers,
           IOptions<DefaultAIOptions> defaultOptions
           ) : base(OllamaConstants.ImplementationName, distributedCache, loggerFactory, providerOptions.Value, defaultOptions.Value, handlers)
    {
    }

    protected override string ProviderName
        => OllamaConstants.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, AICompletionContext context, string deploymentName)
    {
        return new OllamaChatClient(connection.GetEndpoint(), connection.GetDefaultDeploymentName());
    }
}
