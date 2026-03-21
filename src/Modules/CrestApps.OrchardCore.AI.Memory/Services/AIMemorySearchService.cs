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
        => await SearchAsync(userId, [query], requestedTopN, cancellationToken);

    public async Task<IEnumerable<AIMemorySearchResult>> SearchAsync(
        string userId,
        IEnumerable<string> queries,
        int? requestedTopN,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var normalizedQueries = queries?
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Select(query => query.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedQueries is not { Length: > 0 })
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

        var embeddings = await embeddingGenerator.GenerateAsync(normalizedQueries, cancellationToken: cancellationToken);

        if (embeddings is null || embeddings.Count == 0)
        {
            return [];
        }

        var topN = requestedTopN.GetValueOrDefault(settings.TopN);
        topN = topN > 0 ? topN : settings.TopN;
        topN = topN > 0 ? topN : 5;

        var aggregatedResults = new Dictionary<string, AIMemorySearchResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var embedding in embeddings)
        {
            if (embedding?.Vector is null)
            {
                continue;
            }

            var results = await searchService.SearchAsync(indexProfile, embedding.Vector.ToArray(), userId, topN, cancellationToken);

            if (results == null)
            {
                continue;
            }

            foreach (var result in results)
            {
                var key = !string.IsNullOrWhiteSpace(result.MemoryId)
                    ? result.MemoryId
                    : $"{result.Name}|{result.Description}|{result.Content}";

                if (!aggregatedResults.TryGetValue(key, out var existing) || result.Score > existing.Score)
                {
                    aggregatedResults[key] = result;
                }
            }
        }

        return aggregatedResults.Values
            .OrderByDescending(result => result.Score)
            .Take(topN)
            .ToList();
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
