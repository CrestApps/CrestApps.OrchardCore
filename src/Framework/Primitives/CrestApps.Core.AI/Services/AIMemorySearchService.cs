using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Services;

public sealed class AIMemorySearchService : IAIMemorySearchService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly AIMemoryOptions _memoryOptions;
    private readonly ILogger<AIMemorySearchService> _logger;

    public AIMemorySearchService(
        IServiceProvider serviceProvider,
        ISearchIndexProfileStore indexProfileStore,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        IOptions<AIMemoryOptions> memoryOptions,
        ILogger<AIMemorySearchService> logger)
    {
        _serviceProvider = serviceProvider;
        _indexProfileStore = indexProfileStore;
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
        _memoryOptions = memoryOptions.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<AIMemorySearchResult>> SearchAsync(
        string userId,
        IEnumerable<string> queries,
        int? requestedTopN,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogDebug("Skipping AI memory search because the user ID is missing.");
            return [];
        }

        var normalizedQueries = queries?
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Select(query => query.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedQueries is not { Length: > 0 })
        {
            _logger.LogDebug("Skipping AI memory search because no non-empty queries were provided.");
            return [];
        }

        if (string.IsNullOrWhiteSpace(_memoryOptions.IndexProfileName))
        {
            _logger.LogDebug("Skipping AI memory search because no AI Memory index profile is configured.");
            return [];
        }

        var indexProfile = await _indexProfileStore.FindByNameAsync(_memoryOptions.IndexProfileName);

        if (indexProfile is null || !string.Equals(indexProfile.Type, IndexProfileTypes.AIMemory, StringComparison.OrdinalIgnoreCase))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipping AI memory search because configured index profile '{IndexProfileName}' was not found or is not of type '{IndexProfileType}'.",
                    _memoryOptions.IndexProfileName,
                    IndexProfileTypes.AIMemory);
            }
            return [];
        }

        var searchService = _serviceProvider.GetKeyedService<IMemoryVectorSearchService>(indexProfile.ProviderName);

        if (searchService is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipping AI memory search because provider '{ProviderName}' does not have a registered memory vector-search service.",
                    indexProfile.ProviderName);
            }
            return [];
        }

        var embeddingGenerator = await CreateEmbeddingGeneratorAsync(indexProfile, _deploymentManager, _aiClientFactory);

        if (embeddingGenerator is null)
        {
            return [];
        }

        var embeddings = await embeddingGenerator.GenerateAsync(normalizedQueries, cancellationToken: cancellationToken);

        if (embeddings is null || embeddings.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "AI memory search produced no embeddings for configured index profile '{IndexProfileName}'.",
                    indexProfile.Name);
            }
            return [];
        }

        var configuredTopN = _memoryOptions.TopN > 0 ? _memoryOptions.TopN : 5;
        var topN = requestedTopN.GetValueOrDefault(configuredTopN);
        topN = Math.Clamp(topN > 0 ? topN : configuredTopN, 1, 20);

        var aggregatedResults = new Dictionary<string, AIMemorySearchResult>(StringComparer.OrdinalIgnoreCase);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Running AI memory search against profile '{IndexProfileName}' using provider '{ProviderName}' for {QueryCount} query candidate(s).",
                indexProfile.Name,
                indexProfile.ProviderName,
                normalizedQueries.Length);
        }

        foreach (var embedding in embeddings)
        {
            if (embedding.Vector.IsEmpty)
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

    private async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(
        SearchIndexProfile indexProfile,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory)
    {
        if (string.IsNullOrWhiteSpace(indexProfile.EmbeddingDeploymentId))
        {
            _logger.LogWarning("AI memory index profile '{IndexProfileName}' is missing an embedding deployment.", indexProfile.Name);
            return null;
        }

        var deployment = await deploymentManager.FindByIdAsync(indexProfile.EmbeddingDeploymentId);

        if (deployment is null ||
            string.IsNullOrWhiteSpace(deployment.ClientName) ||
            string.IsNullOrWhiteSpace(deployment.ConnectionName) ||
            string.IsNullOrWhiteSpace(deployment.ModelName))
        {
            _logger.LogWarning(
                "AI memory index profile '{IndexProfileName}' could not resolve embedding deployment '{EmbeddingDeploymentId}'.",
                indexProfile.Name,
                indexProfile.EmbeddingDeploymentId);
            return null;
        }

        return await aiClientFactory.CreateEmbeddingGeneratorAsync(
            deployment.ClientName,
            deployment.ConnectionName,
            deployment.ModelName);
    }
}
