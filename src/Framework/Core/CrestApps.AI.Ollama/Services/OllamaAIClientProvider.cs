using CrestApps.AI.Models;
using CrestApps.AI.Services;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace CrestApps.AI.Ollama.Services;

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
}
