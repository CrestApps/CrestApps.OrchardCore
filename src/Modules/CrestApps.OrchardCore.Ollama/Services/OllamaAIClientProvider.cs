using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Ollama.Services;

public sealed class OllamaAIClientProvider : AIClientProviderBase
{
    protected override string ProviderName { get; } = OllamaConstants.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName)
    {
        return new OllamaChatClient(connection.GetEndpoint(), deploymentName);
    }

    protected override IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName)
    {
        return new OllamaEmbeddingGenerator(connection.GetEndpoint(), deploymentName);
    }
}
