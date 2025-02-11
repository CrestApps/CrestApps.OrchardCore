using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

namespace CrestApps.OrchardCore.AI.OpenAI.Services;

public sealed class OpenAICompletionService : NamedAICompletionService
{
    private readonly IDistributedCache _distributedCache;

    public OpenAICompletionService(
        IAIDeploymentStore deploymentStore,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IAIToolsService toolsService,
        IOptions<DefaultAIOptions> defaultOptions,
        ILogger<OpenAICompletionService> logger)
        : base(OpenAIDeploymentProvider.ProviderName, providerOptions.Value, defaultOptions.Value, toolsService, deploymentStore, logger)
    {
        _distributedCache = distributedCache;
    }

    protected override string ProviderName
        => OpenAIDeploymentProvider.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnection connection, AICompletionContext context, string deploymentName)
    {
        var azureClient = new OpenAIClient(connection.GetApiKey())
            .AsChatClient(connection.GetDefaultDeploymentName());

        return new ChatClientBuilder(azureClient)
            .UseDistributedCache(_distributedCache)
            .UseFunctionInvocation(null, (options) =>
            {
                // Set the maximum number of iterations per request to 1 as a safe net to prevent infinite function calling.
                options.MaximumIterationsPerRequest = 1;
            }).Build();
    }
}
