using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace CrestApps.Core.AI.Ollama.Services;

public sealed class OllamaAIClientProvider : AIClientProviderBase
{
    public OllamaAIClientProvider(IServiceProvider serviceProvider)
    : base(serviceProvider)
    {
    }

    protected override string GetProviderName()
        => OllamaConstants.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName)
    {
        return new OllamaApiClient(connection.GetEndpoint(), deploymentName);
    }

    protected override IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName)
    {
        return new OllamaApiClient(connection.GetEndpoint(), deploymentName);
    }

#pragma warning disable MEAI001
    protected override IImageGenerator GetImageGenerator(AIProviderConnectionEntry connection, string deploymentName)
#pragma warning restore MEAI001
    {
        throw new NotSupportedException("Ollama does not support image generation.");
    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    protected override ISpeechToTextClient GetSpeechToTextClient(AIProviderConnectionEntry connection, string deploymentName)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        throw new NotSupportedException("Ollama does not currently support speech-to-text functionality.");
    }
}
