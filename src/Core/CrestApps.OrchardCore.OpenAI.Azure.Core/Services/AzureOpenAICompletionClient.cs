using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using CrestApps.Azure.Core;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Azure.Core.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAICompletionClient : DeploymentAwareAICompletionClient
{
    public AzureOpenAICompletionClient(
       ILoggerFactory loggerFactory,
       IDistributedCache distributedCache,
       IOptions<AIProviderOptions> providerOptions,
       IEnumerable<IAICompletionServiceHandler> handlers,
       IOptions<DefaultAIOptions> defaultOptions,
       INamedModelStore<AIDeployment> deploymentStore
       ) : base(AzureOpenAIConstants.StandardImplementationName,
           distributedCache,
           loggerFactory,
           providerOptions.Value,
           defaultOptions.Value,
           handlers,
           deploymentStore)
    {
    }

    protected override string ProviderName
        => AzureOpenAIConstants.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, AICompletionContext context, string modelName)
    {
        var endpoint = connection.GetEndpoint();

        var azureClient = connection.GetAzureAuthenticationType() switch
        {
            AzureAuthenticationType.ApiKey => new AzureOpenAIClient(endpoint, new ApiKeyCredential(connection.GetApiKey())),
            AzureAuthenticationType.ManagedIdentity => new AzureOpenAIClient(endpoint, new ManagedIdentityCredential()),
            AzureAuthenticationType.Default => new AzureOpenAIClient(endpoint, new DefaultAzureCredential()),
            _ => throw new NotSupportedException("The provided authentication type is not supported.")
        };

        return azureClient
            .GetChatClient(modelName)
            .AsIChatClient();
    }
}
