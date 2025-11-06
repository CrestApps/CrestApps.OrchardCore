using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using CrestApps.Azure.Core;
using CrestApps.Azure.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIClientProvider : AIClientProviderBase
{
    protected override string GetProviderName()
        => AzureOpenAIConstants.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName)
    {
        var azureClient = GetAzureOpenAIClient(connection);

        return azureClient
            .GetChatClient(deploymentName)
            .AsIChatClient();
    }

    protected override IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName)
    {
        var azureClient = GetAzureOpenAIClient(connection);

        return azureClient.GetEmbeddingClient(deploymentName)
            .AsIEmbeddingGenerator();
    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    protected override ISpeechToTextClient GetSpeechToTextClient(AIProviderConnectionEntry connection, string deploymentName)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        var azureClient = GetAzureOpenAIClient(connection);

        // Azure Whisper deployments do not expose the standard /audio/speech-to-text API.
        // Instead, they use /audio/transcriptions, which requires a custom implementation.
        return new AzureSpeechToTextClient(azureClient, deploymentName);
    }

    private static AzureOpenAIClient GetAzureOpenAIClient(AIProviderConnectionEntry connection)
    {
        var endpoint = connection.GetEndpoint();

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
