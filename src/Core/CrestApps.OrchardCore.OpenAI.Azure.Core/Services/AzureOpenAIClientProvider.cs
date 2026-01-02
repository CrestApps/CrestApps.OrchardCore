using System.ClientModel;
using System.ClientModel.Primitives;
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

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    protected override ISpeechToTextClient GetSpeechToTextClient(AIProviderConnectionEntry connection, string deploymentName)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        var azureClient = GetClient(connection, connection.GetEndpoint());

        // Azure Whisper deployments do not expose the standard /audio/speech-to-text API.
        // Instead, they use /audio/transcriptions, which requires a custom implementation.
        return new AzureWhisperSpeechToTextClient(azureClient, deploymentName);
    }

    private AzureOpenAIClient GetClient(AIProviderConnectionEntry connection, Uri endpoint)
    {
        var options = new AzureOpenAIClientOptions
        {
            ClientLoggingOptions = new ClientLoggingOptions
            {
                LoggerFactory = _loggerFactory,
                EnableLogging = connection.GetBooleanOrFalseValue("EnableLogging"),
                EnableMessageLogging = connection.GetBooleanOrFalseValue("EnableMessageLogging"),
                EnableMessageContentLogging = connection.GetBooleanOrFalseValue("EnableMessageContentLogging"),
            },
        };

        var azureClient = connection.GetAzureAuthenticationType() switch
        {
            AzureAuthenticationType.ApiKey => new AzureOpenAIClient(endpoint, new ApiKeyCredential(connection.GetApiKey()), options),
            AzureAuthenticationType.ManagedIdentity => new AzureOpenAIClient(endpoint, new ManagedIdentityCredential(), options),
            AzureAuthenticationType.Default => new AzureOpenAIClient(endpoint, new DefaultAzureCredential(), options),
            _ => throw new NotSupportedException("The provided authentication type is not supported.")
        };

        return azureClient;
    }
}
