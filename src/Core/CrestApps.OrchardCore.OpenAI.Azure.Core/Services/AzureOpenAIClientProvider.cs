using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using CrestApps.Azure.Core;
using CrestApps.Azure.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIClientProvider : AIClientProviderBase
{
    private readonly ILoggerFactory _loggerFactory;

    protected override string GetProviderName()
        => AzureOpenAIConstants.ProviderName;

    public AzureOpenAIClientProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName)
    {
        return GetClient(connection, connection.GetEndpoint())
            .GetChatClient(deploymentName)
            .AsIChatClient();
    }

    protected override IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName)
    {
        var endpoint = connection.GetEndpoint();

        return GetClient(connection, endpoint)
            .GetEmbeddingClient(deploymentName)
            .AsIEmbeddingGenerator();
    }

    private static AzureOpenAIClient GetClient(AIProviderConnectionEntry connection, Uri endpoint)
    {
        var azureClient = connection.GetAzureAuthenticationType() switch
        {
            AzureAuthenticationType.ApiKey => new AzureOpenAIClient(endpoint, new ApiKeyCredential(connection.GetApiKey())),
            AzureAuthenticationType.ManagedIdentity => new AzureOpenAIClient(endpoint, new ManagedIdentityCredential()),
            AzureAuthenticationType.Default => new AzureOpenAIClient(endpoint, new DefaultAzureCredential()),
            _ => throw new NotSupportedException("The provided authentication type is not supported.")
        };

        return azureClient;
    }
}
