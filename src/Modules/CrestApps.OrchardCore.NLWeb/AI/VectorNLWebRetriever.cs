using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.NLWeb.AI;

/// <summary>
/// Finds content items by meaning: embeds the question and searches the vector index of the AI data source the NLWeb
/// settings select. It is registered only when the AI Data Sources feature is enabled.
/// </summary>
internal sealed class VectorNLWebRetriever : INLWebRetriever
{
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorNLWebRetriever"/> class.
    /// </summary>
    /// <param name="dataSourceStore">The AI data source store.</param>
    /// <param name="indexProfileStore">The search index store.</param>
    /// <param name="deploymentManager">The AI deployment manager.</param>
    /// <param name="clientFactory">The AI client factory.</param>
    /// <param name="serviceProvider">The service provider, which gives the vector search of the index's provider, a
    /// service registered under the provider's name.</param>
    /// <param name="logger">The logger.</param>
    public VectorNLWebRetriever(
        IAIDataSourceStore dataSourceStore,
        IIndexProfileStore indexProfileStore,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory,
        IServiceProvider serviceProvider,
        ILogger<VectorNLWebRetriever> logger)
    {
        _dataSourceStore = dataSourceStore;
        _indexProfileStore = indexProfileStore;
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => NLWebConstants.Retrievers.Vector;

    /// <inheritdoc/>
    public Task<bool> IsEnabledAsync(NLWebAskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(!string.IsNullOrEmpty(NLWebSettingsQueryProcessor.GetSettings(context).DataSourceId));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NLWebCandidate>> RetrieveAsync(NLWebAskContext context, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = NLWebSettingsQueryProcessor.GetSettings(context);
        var dataSource = await _dataSourceStore.FindByIdAsync(settings.DataSourceId, cancellationToken);

        if (dataSource is null || string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName))
        {
            _logger.LogWarning("The AI data source '{DataSourceId}' selected for NLWeb was not found or has no knowledge base index.", settings.DataSourceId);

            return [];
        }

        var indexProfile = await _indexProfileStore.FindByNameAsync(dataSource.AIKnowledgeBaseIndexProfileName);

        if (indexProfile is null)
        {
            return [];
        }

        var contentManager = _serviceProvider.GetKeyedService<IDataSourceContentManager>(indexProfile.ProviderName);
        var deploymentName = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile).GetEmbeddingDeploymentName();
        var deployment = string.IsNullOrEmpty(deploymentName)
            ? null
            : await _deploymentManager.FindByNameAsync(deploymentName, cancellationToken);

        if (contentManager is null || deployment is null)
        {
            _logger.LogWarning("The knowledge base index '{IndexName}' of the AI data source selected for NLWeb cannot be searched by meaning.", indexProfile.Name);

            return [];
        }

        var generator = await _clientFactory.CreateEmbeddingGeneratorAsync(deployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(purpose: NLWebChatClientFactory.UsagePurpose));

        var embedding = await generator.GenerateVectorAsync(context.QueryText, cancellationToken: cancellationToken);

        // The index holds chunks, so several chunks of one content item can match; ask for more to fill the list.
        var matches = await contentManager.SearchAsync(
            indexProfile.ToIndexProfileInfo(),
            embedding.ToArray(),
            dataSource.ItemId,
            count * 2,
            cancellationToken: cancellationToken);

        var candidates = new List<NLWebCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var match in matches ?? [])
        {
            if (string.IsNullOrEmpty(match.ReferenceId) ||
                !string.Equals(match.ReferenceType, AIConstants.DataSourceReferenceTypes.Content, StringComparison.OrdinalIgnoreCase) ||
                !seen.Add(match.ReferenceId))
            {
                continue;
            }

            candidates.Add(new NLWebCandidate(match.ReferenceId, match.Score, Name));

            if (candidates.Count == count)
            {
                break;
            }
        }

        return candidates;
    }
}
