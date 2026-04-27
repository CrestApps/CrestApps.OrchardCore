using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Handlers;

/// <summary>
/// Base class for index profile handlers that manage AI document indexing operations.
/// </summary>
public abstract class AIDocumentIndexProfileHandlerBase : IndexProfileHandlerBase
{
    /// <summary>
    /// Gets the name of the provider this handler is responsible for.
    /// </summary>
    protected string ProviderName { get; }

    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDocumentIndexProfileHandlerBase"/> class.
    /// </summary>
    /// <param name="providerName">The name of the indexing provider this handler manages.</param>
    /// <param name="deploymentManager">The deployment manager for resolving AI deployments.</param>
    /// <param name="aiClientFactory">The factory for creating AI clients.</param>
    /// <param name="logger">The logger instance.</param>
    public AIDocumentIndexProfileHandlerBase(
        string providerName,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        ILogger logger)
    {
        ProviderName = providerName;
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
        _logger = logger;
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine embedding dimensions dynamically for index profile '{IndexProfileId}'. Falling back to default dimensions ({DefaultDimensions}).", indexProfile.Id, defaultDimensions);
        }

        return defaultDimensions;
    }

    protected bool CanHandle(IndexProfile indexProfile)
    {
        return string.Equals(AIConstants.AIDocumentsIndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ProviderName, indexProfile.ProviderName, StringComparison.OrdinalIgnoreCase);
    }
}
