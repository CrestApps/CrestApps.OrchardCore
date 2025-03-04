using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
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
       IAIToolsService toolsService,
       IOptions<DefaultAIOptions> defaultOptions,
       INamedModelStore<AIDeployment> deploymentStore
       ) : base(AzureOpenAIConstants.StandardImplementationName, distributedCache, loggerFactory, providerOptions.Value, defaultOptions.Value, toolsService, deploymentStore)
    {
    }

    protected override string ProviderName
        => AzureOpenAIConstants.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, AICompletionContext context, string modelName)
    {
        var endpoint = new Uri($"https://{connection.GetAccountName()}.openai.azure.com/");

        var authenticationTypeString = connection.GetStringValue("AuthenticationType");

        if (string.IsNullOrEmpty(authenticationTypeString) ||
            !Enum.TryParse<AzureAuthenticationType>(authenticationTypeString, true, out var authenticationType))
        {
            authenticationType = AzureAuthenticationType.Default;
        }

        var azureClient = authenticationType switch
        {
            AzureAuthenticationType.ApiKey => new AzureOpenAIClient(endpoint, new ApiKeyCredential(connection.GetApiKey())),
            AzureAuthenticationType.ManagedIdentity => new AzureOpenAIClient(endpoint, new ManagedIdentityCredential()),
            _ => new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
        };

        return azureClient.AsChatClient(modelName);
    }
}
