using Azure;
using Azure.AI.Inference;
using Azure.Identity;
using CrestApps.Azure.Core;
using CrestApps.Azure.Core.Models;
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
        => AzureAIInferenceConstants.ClientName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName)
    {
        var endpoint = connection.GetEndpoint();
        var identityId = connection.GetIdentityId();

        var client = connection.GetAzureAuthenticationType() switch
        {
            AzureAuthenticationType.ApiKey => new ChatCompletionsClient(endpoint, new AzureKeyCredential(connection.GetApiKey())),
            AzureAuthenticationType.ManagedIdentity => new ChatCompletionsClient(endpoint, new ManagedIdentityCredential(string.IsNullOrEmpty(identityId) ? ManagedIdentityId.SystemAssigned : ManagedIdentityId.FromUserAssignedClientId(identityId))),
            AzureAuthenticationType.Default => new ChatCompletionsClient(endpoint, new DefaultAzureCredential()),
            _ => throw new NotSupportedException("The provided authentication type is not supported.")
        };

        return client.AsIChatClient(deploymentName);
    }

    protected override IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName)
    {
        var endpoint = connection.GetEndpoint();
        var identityId = connection.GetIdentityId();

        var client = connection.GetAzureAuthenticationType() switch
        {
            AzureAuthenticationType.ApiKey => new EmbeddingsClient(endpoint, new AzureKeyCredential(connection.GetApiKey())),
            AzureAuthenticationType.ManagedIdentity => new EmbeddingsClient(endpoint, new ManagedIdentityCredential(string.IsNullOrEmpty(identityId) ? ManagedIdentityId.SystemAssigned : ManagedIdentityId.FromUserAssignedClientId(identityId))),
            AzureAuthenticationType.Default => new EmbeddingsClient(endpoint, new DefaultAzureCredential()),
            _ => throw new NotSupportedException("The provided authentication type is not supported.")
        };

        return client.AsIEmbeddingGenerator();
    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    protected override IImageGenerator GetImageGenerator(AIProviderConnectionEntry connection, string deploymentName)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        throw new NotSupportedException("Azure AI Inference does not support image generation.");
    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    protected override ISpeechToTextClient GetSpeechToTextClient(AIProviderConnectionEntry connection, string deploymentName)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        throw new NotSupportedException("Azure AI Inference does not currently support speech-to-text functionality.");
    }
}
