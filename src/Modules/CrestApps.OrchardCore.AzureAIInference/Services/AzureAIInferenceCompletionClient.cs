using Azure;
using Azure.AI.Inference;
using Azure.Core;
using Azure.Identity;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AzureAIInference.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AzureAIInference.Services;

public sealed class AzureAIInferenceCompletionClient : DeploymentAwareAICompletionClient
{
    public AzureAIInferenceCompletionClient(
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IAIToolsService toolsService,
        IOptions<DefaultAIOptions> defaultOptions,
        INamedModelStore<AIDeployment> deploymentStore
        ) : base(AzureAIInferenceConstants.ImplementationName, distributedCache, loggerFactory, providerOptions.Value, defaultOptions.Value, toolsService, deploymentStore)
    {
    }

    protected override string ProviderName
        => AzureAIInferenceConstants.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, AICompletionContext context, string modelName)
    {
        var authenticationTypeString = connection.GetStringValue("AuthenticationType");

        if (string.IsNullOrEmpty(authenticationTypeString) ||
            !Enum.TryParse<AzureAuthenticationType>(authenticationTypeString, true, out var authenticationType))
        {
            authenticationType = AzureAuthenticationType.Default;
        }

        if (authenticationType == AzureAuthenticationType.ApiKey)
        {
            return new ChatCompletionsClient(
                endpoint: new Uri("https://models.inference.ai.azure.com"),
                credential: new AzureKeyCredential(connection.GetApiKey()))
                .AsChatClient(connection.GetDefaultDeploymentName());
        }

        TokenCredential credential = authenticationType == AzureAuthenticationType.ManagedIdentity
            ? new ManagedIdentityCredential()
            : new DefaultAzureCredential();

        return new ChatCompletionsClient(
            endpoint: new Uri("https://models.inference.ai.azure.com"),
            credential: credential)
            .AsChatClient(connection.GetDefaultDeploymentName());
    }
}
