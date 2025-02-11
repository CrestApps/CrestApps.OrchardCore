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
    private readonly IDistributedCache _distributedCache;

    public OllamaAIChatCompletionService(
        IAIDeploymentStore deploymentStore,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IAIToolsService toolsService,
        IOptions<DefaultAIOptions> defaultOptions,
        ILogger<OllamaAIChatCompletionService> logger)
        : base(OllamaProfileSource.Key, providerOptions, toolsService, defaultOptions, deploymentStore, logger)
    {
        _distributedCache = distributedCache;
    }

    protected override string ProviderName => OllamaProfileSource.Key;

    protected override IChatClient GetChatClient(AIProviderConnection connection, AIChatCompletionContext context, string deploymentName)
    {
        var endpoint = new Uri(connection.GetStringValue("Endpoint"));

        var azureClient = new OllamaChatClient(endpoint, connection.GetDefaultDeploymentName());

        return new ChatClientBuilder(azureClient)
            .UseDistributedCache(_distributedCache)
            .UseFunctionInvocation(null, (options) =>
            {
                // Set the maximum number of iterations per request to 1 as a safe net to prevent infinite function calling.
                options.MaximumIterationsPerRequest = 1;
            }).Build();
    }
}
