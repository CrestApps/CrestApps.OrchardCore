using CrestApps.Azure.Core;
using CrestApps.Azure.Core.Models;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

/// <summary>
/// Client provider for "AzureSpeech" deployments.
/// Uses the Azure Speech SDK for speech-to-text and text-to-speech.
/// </summary>
public sealed class AzureSpeechClientProvider : IAIClientProvider
{
    private readonly ILoggerFactory _loggerFactory;

    public AzureSpeechClientProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public bool CanHandle(string providerName)
        => string.Equals(AzureOpenAIConstants.AzureSpeechProviderName, providerName, StringComparison.OrdinalIgnoreCase);

    public ValueTask<IChatClient> GetChatClientAsync(AIProviderConnectionEntry connection, string deploymentName = null)
        => throw new NotSupportedException("Azure AI Speech deployments only support speech services.");

    public ValueTask<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingGeneratorAsync(AIProviderConnectionEntry connection, string deploymentName = null)
        => throw new NotSupportedException("Azure AI Speech deployments only support speech services.");

#pragma warning disable MEAI001
    public ValueTask<IImageGenerator> GetImageGeneratorAsync(AIProviderConnectionEntry connection, string deploymentName = null)
        => throw new NotSupportedException("Azure AI Speech deployments only support speech services.");

#pragma warning disable MEAI001 // Text-to-speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.
    public ValueTask<ISpeechToTextClient> GetSpeechToTextClientAsync(AIProviderConnectionEntry connection, string deploymentName = null)
    {
        var (endpoint, authType, apiKey, identityId) = ExtractConnectionParams(connection);
        var logger = _loggerFactory.CreateLogger<AzureSpeechServiceSpeechToTextClient>();

        var client = new AzureSpeechServiceSpeechToTextClient(
            endpoint,
            authType,
            apiKey,
            identityId,
            logger);

        return ValueTask.FromResult<ISpeechToTextClient>(client);
    }
#pragma warning restore MEAI001

#pragma warning disable MEAI001 // Text-to-speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.
    public ValueTask<ITextToSpeechClient> GetTextToSpeechClientAsync(AIProviderConnectionEntry connection, string deploymentName = null)
    {
        var (endpoint, authType, apiKey, identityId) = ExtractConnectionParams(connection);
        var logger = _loggerFactory.CreateLogger<AzureSpeechServiceTextToSpeechClient>();

        var client = new AzureSpeechServiceTextToSpeechClient(
            endpoint,
            authType,
            apiKey,
            identityId,
            logger);

        return ValueTask.FromResult<ITextToSpeechClient>(client);
    }
#pragma warning restore MEAI001 // Text-to-speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.

    public async Task<SpeechVoice[]> GetSpeechVoicesAsync(AIProviderConnectionEntry connection, string deploymentName = null)
    {
        var (endpoint, authType, apiKey, identityId) = ExtractConnectionParams(connection);
        var logger = _loggerFactory.CreateLogger<AzureSpeechServiceTextToSpeechClient>();

        using var client = new AzureSpeechServiceTextToSpeechClient(
            endpoint,
            authType,
            apiKey,
            identityId,
            logger);

        return await client.GetVoicesAsync();
    }

    private static (Uri endpoint, AzureAuthenticationType authType, string apiKey, string identityId) ExtractConnectionParams(AIProviderConnectionEntry connection)
    {
        var endpoint = connection.GetEndpoint();
        var authType = connection.GetAzureAuthenticationType();
        var apiKey = authType == AzureAuthenticationType.ApiKey ? connection.GetApiKey() : null;
        var identityId = connection.GetIdentityId();

        return (endpoint, authType, apiKey, identityId);
    }
}
