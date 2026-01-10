using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;

public abstract class ChatInteractionsIndexProfileHandlerBase : IndexProfileHandlerBase
{
    protected string ProviderName { get; }

    private readonly IAIClientFactory _aiClientFactory;

    public ChatInteractionsIndexProfileHandlerBase(string providerName, IAIClientFactory aiClientFactory)
    {
        ProviderName = providerName;
        _aiClientFactory = aiClientFactory;
    }

    protected async Task<int> GetEmbeddingDimensionsAsync(ChatInteractionIndexProfileMetadata interactionMetadata)
    {
        // Default to 1536 (OpenAI text-embedding-ada-002) if we can't determine dynamically
        const int defaultDimensions = 1536;

        // Use the embedding connection configured in the index profile
        if (string.IsNullOrEmpty(interactionMetadata?.EmbeddingProviderName) ||
            string.IsNullOrEmpty(interactionMetadata.EmbeddingConnectionName) ||
            string.IsNullOrEmpty(interactionMetadata.EmbeddingDeploymentName))
        {
            return defaultDimensions;
        }

        try
        {
            var embeddingGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(
                interactionMetadata.EmbeddingProviderName,
                interactionMetadata.EmbeddingConnectionName,
                interactionMetadata.EmbeddingDeploymentName);

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
        return string.Equals(ProviderName, indexProfile.ProviderName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ChatInteractionsConstants.IndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }
}
