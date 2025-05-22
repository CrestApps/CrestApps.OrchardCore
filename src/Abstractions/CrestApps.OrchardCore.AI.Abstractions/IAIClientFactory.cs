using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

public interface IAIClientFactory
{
    ValueTask<IChatClient> CreateChatClientAsync(string providerName, string connectionName, string deploymentName);

    ValueTask<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(string providerName, string connectionName, string deploymentName);
}
