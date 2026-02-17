using CrestApps.OrchardCore.AI.Core.Models;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public abstract class DataSourceIndexProfileHandlerBase : IndexProfileHandlerBase
{
    protected string ProviderName { get; }

    private readonly IAIClientFactory _aiClientFactory;

    protected DataSourceIndexProfileHandlerBase(string providerName, IAIClientFactory aiClientFactory)
    {
        ProviderName = providerName;
        _aiClientFactory = aiClientFactory;
    }

    protected async Task<int> GetEmbeddingDimensionsAsync(DataSourceIndexProfileMetadata metadata)
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

            if (embeddingGenerator == null)
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
            // If we can't determine dimensions dynamically, fall back to default.
        }

        return defaultDimensions;
    }

    protected bool CanHandle(IndexProfile indexProfile)
    {
        return string.Equals(DataSourceConstants.IndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ProviderName, indexProfile.ProviderName, StringComparison.OrdinalIgnoreCase);
    }
}
