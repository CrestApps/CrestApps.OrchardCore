using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Memory.Handlers;

/// <summary>
/// Represents the AI memory index profile handler base.
/// </summary>
public abstract class AIMemoryIndexProfileHandlerBase : IndexProfileHandlerBase
{
    protected string ProviderName { get; }

    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;

    protected AIMemoryIndexProfileHandlerBase(
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
        const int defaultDimensions = 1536;
        var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);

        try
        {
            var embeddingGenerator = await EmbeddingDeploymentResolver.CreateEmbeddingGeneratorAsync(
                _deploymentManager,
                _aiClientFactory,
                metadata);

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
