using Azure.AI.OpenAI;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIChatCompletionService : NamedAICompletionService
{
    private readonly IDistributedCache _distributedCache;

    public AzureOpenAIChatCompletionService(
        IAIDeploymentStore deploymentStore,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IAIToolsService toolsService,
        IOptions<DefaultAIOptions> defaultOptions,
        ILogger<AzureOpenAIChatCompletionService> logger)
        : base(AzureProfileSource.Key, providerOptions.Value, defaultOptions.Value, toolsService, deploymentStore, logger)
    {
        _distributedCache = distributedCache;
    }

    protected override string ProviderName
        => AzureOpenAIConstants.AzureProviderName;

    protected override IChatClient GetChatClient(AIProviderConnection connection, AIChatCompletionContext context, string modelName)
    {
        var endpoint = new Uri($"https://{connection.GetAccountName()}.openai.azure.com/");

        var azureClient = new AzureOpenAIClient(endpoint, connection.GetApiKeyCredential());

        return azureClient
            .AsChatClient(modelName)
            .AsBuilder()
            .UseDistributedCache(_distributedCache)
            .UseFunctionInvocation(null, (options) =>
            {
                // Set the maximum number of iterations per request to 1 as a safe net to prevent infinite function calling.
                options.MaximumIterationsPerRequest = 1;
            }).Build();
    }
}
