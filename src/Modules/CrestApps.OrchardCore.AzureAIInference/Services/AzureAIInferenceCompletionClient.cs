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

public sealed class AzureAIInferenceCompletionClient : DeploymentNamedAICompletionClient
{
    public AzureAIInferenceCompletionClient(
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IAIToolsService toolsService,
        IOptions<DefaultAIOptions> defaultOptions,
        IAIDeploymentStore deploymentStore
        ) : base(AzureAIInferenceDeploymentProvider.ProviderName, distributedCache, loggerFactory, providerOptions.Value, defaultOptions.Value, toolsService, deploymentStore)
    {
    }

    protected override string ProviderName
        => AzureAIInferenceDeploymentProvider.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnection connection, AICompletionContext context, string modelName)
    {
        var client = new ChatCompletionsClient(
            endpoint: new Uri("https://models.inference.ai.azure.com"),
            credential: new AzureKeyCredential(connection.GetApiKey()))
        .AsChatClient(connection.GetDefaultDeploymentName());

        return client;
    }
}
