using System.ClientModel;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class OpenAISettingsAICompletionClient : DeploymentAwareAICompletionClient
{
    public OpenAISettingsAICompletionClient(
           ILoggerFactory loggerFactory,
           IDistributedCache distributedCache,
           IOptions<AIProviderOptions> providerOptions,
           IAIToolsService toolsService,
           IOptions<DefaultAIOptions> defaultOptions,
           IAIDeploymentStore deploymentStore
           ) : base(OpenAIConstants.OpenAISettingsProviderName, distributedCache, loggerFactory, providerOptions.Value, defaultOptions.Value, toolsService, deploymentStore)
    {
    }

    protected override string ProviderName
        => OpenAIConstants.OpenAISettingsProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, AICompletionContext context, string modelName)
    {
        var client = new OpenAIClient(new ApiKeyCredential(connection.GetApiKey()), new OpenAIClientOptions()
        {
            Endpoint = connection.GetEndpoint()
        });

        return client.AsChatClient(modelName);
    }
}
