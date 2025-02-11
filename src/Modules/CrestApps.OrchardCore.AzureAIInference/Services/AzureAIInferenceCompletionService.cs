using Azure;
using Azure.AI.Inference;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AzureAIInference.Services;

public sealed class AzureAIInferenceCompletionService : NamedAICompletionService
{
    private readonly IDistributedCache _distributedCache;

    public AzureAIInferenceCompletionService(
        IOptions<AIProviderOptions> providerOptions,
        IAIToolsService toolsService,
        IOptions<DefaultAIOptions> defaultOptions,
        IAIDeploymentStore deploymentStore,
        IDistributedCache distributedCache,
        ILogger logger)
        : base(AzureAIInferenceDeploymentProvider.ProviderName, providerOptions, toolsService, defaultOptions, deploymentStore, logger)
    {
        _distributedCache = distributedCache;
    }

    protected override string ProviderName
        => AzureAIInferenceDeploymentProvider.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnection connection, AIChatCompletionContext context, string modelName)
    {
        var builder = new ChatCompletionsClient(
        endpoint: new Uri("https://models.inference.ai.azure.com"),
        new AzureKeyCredential(connection.GetApiKey()))
        .AsChatClient(connection.GetDefaultDeploymentName())
        .AsBuilder()
        .UseDistributedCache(_distributedCache)
        .UseFunctionInvocation(null, options =>
        {
            // Set the maximum number of iterations per request to 1 as a safe net to prevent infinite function calling.
            options.MaximumIterationsPerRequest = 1;
        });

        return builder.Build();
    }
}
