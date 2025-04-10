using Azure;
using Azure.AI.Inference;
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

namespace CrestApps.OrchardCore.AzureAIInference.Services;

public sealed class AzureAIInferenceCompletionClient : DeploymentAwareAICompletionClient
{
    public AzureAIInferenceCompletionClient(
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        IOptions<DefaultAIOptions> defaultOptions,
        INamedModelStore<AIDeployment> deploymentStore
        ) : base(AzureAIInferenceConstants.ImplementationName,
            distributedCache,
            loggerFactory,
            providerOptions.Value,
            defaultOptions.Value,
            handlers,
            deploymentStore)
    {
    }

    protected override string ProviderName
        => AzureAIInferenceConstants.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, AICompletionContext context, string modelName)
    {
        var endpoint = connection.GetEndpoint();

        var client = connection.GetAzureAuthenticationType() switch
        {
            AzureAuthenticationType.ApiKey => new ChatCompletionsClient(endpoint, new AzureKeyCredential(connection.GetApiKey())),
            AzureAuthenticationType.ManagedIdentity => new ChatCompletionsClient(endpoint, new ManagedIdentityCredential()),
            AzureAuthenticationType.Default => new ChatCompletionsClient(endpoint, new DefaultAzureCredential()),
            _ => throw new NotSupportedException("The provided authentication type is not supported.")
        };

        return client.AsIChatClient(connection.GetDefaultDeploymentName());
    }
}
