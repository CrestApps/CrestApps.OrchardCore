using CrestApps.AI.Clients;
using CrestApps.AI.Deployments;
using CrestApps.AI.Memory;
using CrestApps.AI.Models;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Services;

public sealed class AIMemorySearchService : IAIMemorySearchService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly AIMemoryOptions _memoryOptions;
    private readonly ILogger<AIMemorySearchService> _logger;

    public AIMemorySearchService(
        IServiceProvider serviceProvider,
        IAIClientFactory aiClientFactory,
        IOptions<AIMemoryOptions> memoryOptions,
        ILogger<AIMemorySearchService> logger)
    {
        _serviceProvider = serviceProvider;
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

        if (string.IsNullOrWhiteSpace(_memoryOptions.IndexProfileName))
        {
            return [];
        }

        var indexProfileStore = _serviceProvider.GetService<ISearchIndexProfileStore>();

        if (indexProfileStore is null)
        {
            return [];
        }

        var indexProfile = await indexProfileStore.FindByNameAsync(_memoryOptions.IndexProfileName);

        if (indexProfile is null || !string.Equals(indexProfile.Type, IndexProfileTypes.AIMemory, StringComparison.OrdinalIgnoreCase))
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

        var configuredTopN = _memoryOptions.TopN > 0 ? _memoryOptions.TopN : 5;
        var topN = requestedTopN.GetValueOrDefault(configuredTopN);
        topN = Math.Clamp(topN > 0 ? topN : configuredTopN, 1, 20);

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

    private async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(SearchIndexProfile indexProfile)
    {
        if (string.IsNullOrWhiteSpace(indexProfile.EmbeddingDeploymentId))
        {
            _logger.LogWarning("AI memory index profile '{IndexProfileName}' is missing an embedding deployment.", indexProfile.Name);
            return null;
        }

        var deploymentManager = _serviceProvider.GetService<IAIDeploymentManager>();

        if (deploymentManager is null)
        {
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

        return await _aiClientFactory.CreateEmbeddingGeneratorAsync(
            deployment.ClientName,
            deployment.ConnectionName,
            deployment.ModelName);
    }
}
