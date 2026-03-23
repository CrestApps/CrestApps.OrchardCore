using CrestApps.AI;
using CrestApps.OrchardCore.AI.Memory.Models;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Memory.Handlers;

public abstract class AIMemoryIndexProfileHandlerBase : IndexProfileHandlerBase
{
    protected string ProviderName { get; }

    private readonly IAIClientFactory _aiClientFactory;

    protected AIMemoryIndexProfileHandlerBase(string providerName, IAIClientFactory aiClientFactory)
    {
        ProviderName = providerName;
        _aiClientFactory = aiClientFactory;
    }

    protected async Task<int> GetEmbeddingDimensionsAsync(AIMemoryIndexProfileMetadata metadata)
    {
        const int defaultDimensions = 1536;

        if (string.IsNullOrEmpty(metadata?.EmbeddingProviderName) ||
            string.IsNullOrEmpty(metadata.EmbeddingConnectionName) ||
            string.IsNullOrEmpty(metadata.EmbeddingDeploymentName))
        {
            return defaultDimensions;
        }

        try
        {
            var embeddingGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(
                metadata.EmbeddingProviderName,
                metadata.EmbeddingConnectionName,
                metadata.EmbeddingDeploymentName);

            if (embeddingGenerator is null)
            {
                return defaultDimensions;
            }

            var embedding = await embeddingGenerator.GenerateAsync(["Sample"]);

            if (embedding?.Count > 0 && embedding[0].Vector.Length > 0)
            {
                return embedding[0].Vector.Length;
            }
        }
        catch (Exception)
        {
        }

        return defaultDimensions;
    }

    protected bool CanHandle(IndexProfile indexProfile)
        => string.Equals(indexProfile.Type, MemoryConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(indexProfile.ProviderName, ProviderName, StringComparison.OrdinalIgnoreCase);
}
