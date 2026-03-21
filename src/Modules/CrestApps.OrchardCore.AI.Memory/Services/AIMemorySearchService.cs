using CrestApps.OrchardCore.AI.Memory.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Services;

internal sealed class AIMemorySearchService
{
    private readonly ISiteService _siteService;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly ILogger _logger;

    public AIMemorySearchService(
        ISiteService siteService,
        IIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider,
        IAIClientFactory aiClientFactory,
        ILogger<AIMemorySearchService> logger)
    {
        _siteService = siteService;
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;
        _aiClientFactory = aiClientFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<AIMemorySearchResult>> SearchAsync(
        string userId,
        string query,
        int? requestedTopN,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var settings = await _siteService.GetSettingsAsync<AIMemorySettings>();

        if (string.IsNullOrEmpty(settings.IndexProfileName))
        {
            return [];
        }

        var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);

        if (indexProfile is null)
        {
            return [];
        }

        var searchService = _serviceProvider.GetKeyedService<IMemoryVectorSearchService>(indexProfile.ProviderName);

        if (searchService is null)
        {
            return [];
        }

        var embeddingGenerator = await CreateEmbeddingGeneratorAsync(indexProfile);

        if (embeddingGenerator is null)
        {
            return [];
        }

        var embeddings = await embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);

        if (embeddings is null || embeddings.Count == 0 || embeddings[0]?.Vector is null)
        {
            return [];
        }

        var topN = requestedTopN.GetValueOrDefault(settings.TopN);

        if (topN <= 0)
        {
            topN = settings.TopN;
        }

        if (topN <= 0)
        {
            topN = 5;
        }

        return await searchService.SearchAsync(indexProfile, embeddings[0].Vector.ToArray(), userId, topN, cancellationToken);
    }

    private async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(IndexProfile indexProfile)
    {
        var metadata = indexProfile.As<AIMemoryIndexProfileMetadata>();

        if (string.IsNullOrEmpty(metadata?.EmbeddingProviderName) ||
            string.IsNullOrEmpty(metadata.EmbeddingConnectionName) ||
            string.IsNullOrEmpty(metadata.EmbeddingDeploymentName))
        {
            _logger.LogWarning("AI memory index profile '{IndexProfileName}' is missing embedding configuration.", indexProfile.Name);
            return null;
        }

        return await _aiClientFactory.CreateEmbeddingGeneratorAsync(
            metadata.EmbeddingProviderName,
            metadata.EmbeddingConnectionName,
            metadata.EmbeddingDeploymentName);
    }
}
