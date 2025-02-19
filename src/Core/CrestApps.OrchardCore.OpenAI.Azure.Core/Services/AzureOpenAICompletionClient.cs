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

public sealed class AzureOpenAICompletionClient : DeploymentNamedAICompletionClient
{
    public AzureOpenAICompletionClient(
       ILoggerFactory loggerFactory,
       IDistributedCache distributedCache,
       IOptions<AIProviderOptions> providerOptions,
       IAIToolsService toolsService,
       IOptions<DefaultAIOptions> defaultOptions,
       IAIDeploymentStore deploymentStore
       ) : base(AzureProfileSource.Key, distributedCache, loggerFactory, providerOptions.Value, defaultOptions.Value, toolsService, deploymentStore)
    {
    }

    protected override string ProviderName
        => AzureOpenAIConstants.AzureProviderName;

    protected override IChatClient GetChatClient(AIProviderConnection connection, AICompletionContext context, string modelName)
    {
        var endpoint = new Uri($"https://{connection.GetAccountName()}.openai.azure.com/");

        var azureClient = new AzureOpenAIClient(endpoint, connection.GetApiKeyCredential());

        return azureClient.AsChatClient(modelName);
    }
}
