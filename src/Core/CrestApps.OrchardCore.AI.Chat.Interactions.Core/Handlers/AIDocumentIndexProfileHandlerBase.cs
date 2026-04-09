using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Handlers;

public abstract class AIDocumentIndexProfileHandlerBase : IndexProfileHandlerBase
{
    protected string ProviderName { get; }

    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;

    public AIDocumentIndexProfileHandlerBase(
        string providerName,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory)
    {
        ProviderName = providerName;
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
    }

    protected async Task<int> GetEmbeddingDimensionsAsync(IndexProfile indexProfile)
    {
        // Default to 1536 (OpenAI text-embedding-ada-002) if we can't determine dynamically
        const int defaultDimensions = 1536;

        var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);

        try
        {
            var embeddingGenerator = await EmbeddingDeploymentResolver.CreateEmbeddingGeneratorAsync(
                _deploymentManager,
                _aiClientFactory,
                metadata);

            if (embeddingGenerator == null)
            {
                return defaultDimensions;
            }

            // Generate embedding for a sample text to determine dimensions
            var embedding = await embeddingGenerator.GenerateAsync(["Sample"]);

            if (embedding?.Count > 0 && embedding[0].Vector.Length > 0)
            {
                return embedding[0].Vector.Length;
            }
        }
        catch (Exception)
        {
            // If we can't determine dimensions dynamically (e.g., invalid connection or API error),
            // silently fall back to default dimensions.
        }

        return defaultDimensions;
    }

    protected bool CanHandle(IndexProfile indexProfile)
    {
        return string.Equals(AIConstants.AIDocumentsIndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ProviderName, indexProfile.ProviderName, StringComparison.OrdinalIgnoreCase);
    }
}
