using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// The base of an index profile handler for an AI index that stores embeddings (data sources, documents, memory):
/// it says which profiles it handles, by index type and provider, and resolves the embedding dimensions of a profile.
/// </summary>
/// <remarks>
/// <see cref="DataSourceIndexProfileHandlerBase"/>, <see cref="AIMemoryIndexProfileHandlerBase"/> and the documents
/// base differ only in the index type they pass.
/// </remarks>
public abstract class EmbeddingIndexProfileHandlerBase : IndexProfileHandlerBase
{
    /// <summary>
    /// The dimensions used when the embedding deployment can't tell: those of OpenAI's text-embedding-ada-002.
    /// </summary>
    public const int DefaultEmbeddingDimensions = 1536;

    private readonly string _indexType;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingIndexProfileHandlerBase"/> class.
    /// </summary>
    /// <param name="indexType">The index profile type the handler manages.</param>
    /// <param name="providerName">The name of the indexing provider the handler manages.</param>
    /// <param name="deploymentManager">The deployment manager for resolving AI deployments.</param>
    /// <param name="aiClientFactory">The factory for creating AI clients.</param>
    /// <param name="logger">The logger the embedding failures are written to.</param>
    protected EmbeddingIndexProfileHandlerBase(
        string indexType,
        string providerName,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(indexType);

        _indexType = indexType;
        ProviderName = providerName;
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Gets the name of the provider this handler is responsible for.
    /// </summary>
    protected string ProviderName { get; }

    /// <summary>
    /// Resolves the embedding dimensions of an index profile by embedding a sample text with the profile's embedding
    /// deployment, and falls back to <see cref="DefaultEmbeddingDimensions"/> when that is not possible.
    /// </summary>
    /// <param name="indexProfile">The index profile.</param>
    /// <returns>The embedding dimensions.</returns>
    protected async Task<int> GetEmbeddingDimensionsAsync(IndexProfile indexProfile)
    {
        var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);

        try
        {
            var embeddingDeploymentName = metadata.GetEmbeddingDeploymentName();

            if (string.IsNullOrWhiteSpace(embeddingDeploymentName))
            {
                return DefaultEmbeddingDimensions;
            }

            var deployment = await _deploymentManager.FindByNameAsync(embeddingDeploymentName);

            if (deployment is null)
            {
                return DefaultEmbeddingDimensions;
            }

            var embeddingGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(deployment, builder => builder.UseDefaultResilience().UseUsageLabels(purpose: AIUsagePurposes.Indexing));

            if (embeddingGenerator is null)
            {
                return DefaultEmbeddingDimensions;
            }

            var embedding = await embeddingGenerator.GenerateAsync(["Sample"]);

            if (embedding?.Count > 0 && embedding[0].Vector.Length > 0)
            {
                return embedding[0].Vector.Length;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine embedding dimensions dynamically for index profile '{IndexProfileId}'. Falling back to default dimensions ({DefaultDimensions}).", indexProfile.Id, DefaultEmbeddingDimensions);
        }

        return DefaultEmbeddingDimensions;
    }

    /// <summary>
    /// Says whether this handler manages an index profile: its type and provider match.
    /// </summary>
    /// <param name="indexProfile">The index profile.</param>
    /// <returns><see langword="true"/> when the handler manages the profile.</returns>
    protected bool CanHandle(IndexProfile indexProfile)
        => string.Equals(_indexType, indexProfile.Type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ProviderName, indexProfile.ProviderName, StringComparison.OrdinalIgnoreCase);
}
