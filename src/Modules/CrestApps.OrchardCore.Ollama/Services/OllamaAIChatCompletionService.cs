using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Ollama.Services;

public sealed class OllamaAIChatCompletionService : NamedAICompletionService
{
    public OllamaAIChatCompletionService(
           ILoggerFactory loggerFactory,
           IDistributedCache distributedCache,
           IOptions<AIProviderOptions> providerOptions,
           IAIToolsService toolsService,
           IOptions<DefaultAIOptions> defaultOptions
           ) : base(OllamaProfileSource.Key, distributedCache, loggerFactory, providerOptions.Value, defaultOptions.Value, toolsService)
    {
    }

    protected override string ProviderName
        => OllamaProfileSource.Key;

    protected override IChatClient GetChatClient(AIProviderConnection connection, AICompletionContext context, string deploymentName)
    {
        var endpoint = new Uri(connection.GetStringValue("Endpoint"));

        return new OllamaChatClient(endpoint, connection.GetDefaultDeploymentName());
    }
}
