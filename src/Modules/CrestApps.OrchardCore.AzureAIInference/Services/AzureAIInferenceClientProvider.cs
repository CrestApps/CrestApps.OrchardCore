using Azure;
using Azure.AI.Inference;
using Azure.Identity;
using CrestApps.Azure.Core;
using CrestApps.Azure.Core.Models;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AzureAIInference.Services;

public sealed class AzureAIInferenceClientProvider : AIClientProviderBase
{
    public AzureAIInferenceClientProvider(IServiceProvider serviceProvider)
    : base(serviceProvider)
    {
    }

    protected override string GetProviderName()
        => AzureAIInferenceConstants.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName)
    {
        var endpoint = connection.GetEndpoint();

        var client = connection.GetAzureAuthenticationType() switch
        {
            AzureAuthenticationType.ApiKey => new ChatCompletionsClient(endpoint, new AzureKeyCredential(connection.GetApiKey())),
            AzureAuthenticationType.ManagedIdentity => new ChatCompletionsClient(endpoint, new ManagedIdentityCredential()),
            AzureAuthenticationType.Default => new ChatCompletionsClient(endpoint, new DefaultAzureCredential()),
            _ => throw new NotSupportedException("The provided authentication type is not supported.")
        };

        return client.AsIChatClient(deploymentName);
    }

    protected override IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName)
    {
        var endpoint = connection.GetEndpoint();

        var client = connection.GetAzureAuthenticationType() switch
        {
            AzureAuthenticationType.ApiKey => new EmbeddingsClient(endpoint, new AzureKeyCredential(connection.GetApiKey())),
            AzureAuthenticationType.ManagedIdentity => new EmbeddingsClient(endpoint, new ManagedIdentityCredential()),
            AzureAuthenticationType.Default => new EmbeddingsClient(endpoint, new DefaultAzureCredential()),
            _ => throw new NotSupportedException("The provided authentication type is not supported.")
        };

        return client.AsIEmbeddingGenerator();
    }

    protected override CrestApps.OrchardCore.AI.IImageGenerator GetImageGenerator(AIProviderConnectionEntry connection, string deploymentName)
    {
        throw new NotSupportedException("Azure AI Inference does not support image generation.");
    }
}
