#pragma warning disable MEAI001 // IImageGenerator is experimental but we intentionally use it

using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace CrestApps.OrchardCore.Ollama.Services;

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

    protected override IImageGenerator GetImageGenerator(AIProviderConnectionEntry connection, string deploymentName)
    {
        throw new NotSupportedException("Ollama does not support image generation.");
    }
}
