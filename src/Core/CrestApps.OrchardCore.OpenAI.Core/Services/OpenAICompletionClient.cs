using System.ClientModel;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

namespace CrestApps.OrchardCore.AI.OpenAI.Services;

public sealed class OpenAICompletionClient : DeploymentAwareAICompletionClient
{
    public OpenAICompletionClient(
       ILoggerFactory loggerFactory,
       IDistributedCache distributedCache,
       IOptions<AIProviderOptions> providerOptions,
       IEnumerable<IAICompletionServiceHandler> handlers,
       IOptions<DefaultAIOptions> defaultOptions,
       INamedModelStore<AIDeployment> deploymentStore
       ) : base(OpenAIConstants.ImplementationName,
           distributedCache,
           loggerFactory,
           providerOptions.Value,
           defaultOptions.Value,
           handlers,
           deploymentStore)
    {
    }

    protected override string ProviderName
        => OpenAIConstants.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, AICompletionContext context, string deploymentName)
    {
        var endpoint = connection.GetEndpoint(false);

        OpenAIClient client;

        if (endpoint is null)
        {
            client = new OpenAIClient(connection.GetApiKey());
        }
        else
        {
            client = new OpenAIClient(new ApiKeyCredential(connection.GetApiKey()), new OpenAIClientOptions
            {
                Endpoint = endpoint,
            });
        }

        return client
            .GetChatClient(connection.GetDefaultDeploymentName())
            .AsIChatClient();
    }
}
