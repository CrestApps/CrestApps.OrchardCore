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

public sealed class AIMemoryIndexingService
{
    private readonly IAIMemoryStore _memoryStore;
    private readonly AIMemoryOptions _memoryOptions;
    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AIMemoryIndexingService> _logger;

    public AIMemoryIndexingService(
        IAIMemoryStore memoryStore,
        IOptions<AIMemoryOptions> memoryOptions,
        ISearchIndexProfileStore indexProfileStore,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        IServiceProvider serviceProvider,
        ILogger<AIMemoryIndexingService> logger)
    {
        _memoryStore = memoryStore;
        _memoryOptions = memoryOptions.Value;
        _indexProfileStore = indexProfileStore;
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task IndexAsync(AIMemoryEntry memory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memory);

        var indexProfile = await GetConfiguredIndexProfileAsync(cancellationToken);

        if (indexProfile is null)
        {
            return;
        }

        var indexManager = _serviceProvider.GetKeyedService<ISearchIndexManager>(indexProfile.ProviderName);
        var documentManager = _serviceProvider.GetKeyedService<ISearchDocumentManager>(indexProfile.ProviderName);

        if (indexManager is null || documentManager is null)
        {
            _logger.LogWarning("Skipping AI memory indexing because provider '{ProviderName}' is not configured for search indexing.", indexProfile.ProviderName);
            return;
        }

        var embeddingGenerator = await CreateEmbeddingGeneratorAsync(indexProfile);

        if (embeddingGenerator is null)
        {
            return;
        }

        var embeddingText = $"Name: {memory.Name}{Environment.NewLine}Description: {memory.Description}{Environment.NewLine}Content: {memory.Content}";
        var embeddings = await embeddingGenerator.GenerateAsync([embeddingText], cancellationToken: cancellationToken);
        var vector = embeddings.Count > 0 && embeddings[0] is { Vector: { } generatedVector }
            ? generatedVector.ToArray()
            : null;

        if (vector is not { Length: > 0 })
        {
            _logger.LogWarning("Skipping AI memory indexing for '{MemoryId}' because no embedding was generated.", memory.ItemId);
            return;
        }

        if (!await indexManager.ExistsAsync(indexProfile, cancellationToken))
        {
            await indexManager.CreateAsync(indexProfile, BuildFields(vector.Length), cancellationToken);
        }

        var document = new IndexDocument
        {
            Id = memory.ItemId,
            Fields = new Dictionary<string, object>
            {
                [MemoryConstants.ColumnNames.MemoryId] = memory.ItemId,
                [MemoryConstants.ColumnNames.UserId] = memory.UserId ?? string.Empty,
                [MemoryConstants.ColumnNames.Name] = memory.Name ?? string.Empty,
                [MemoryConstants.ColumnNames.Description] = memory.Description ?? string.Empty,
                [MemoryConstants.ColumnNames.Content] = memory.Content ?? string.Empty,
                [MemoryConstants.ColumnNames.UpdatedUtc] = memory.UpdatedUtc,
                [MemoryConstants.ColumnNames.Embedding] = vector,
            },
        };

        var indexed = await documentManager.AddOrUpdateAsync(indexProfile, [document], cancellationToken);

        if (!indexed)
        {
            _logger.LogWarning("AI memory indexing reported failure for memory '{MemoryId}' into index '{IndexName}'.", memory.ItemId, indexProfile.IndexFullName);
        }
    }

    public async Task DeleteAsync(IEnumerable<string> memoryIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memoryIds);

        var ids = memoryIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0)
        {
            return;
        }

        var indexProfile = await GetConfiguredIndexProfileAsync(cancellationToken);

        if (indexProfile is null)
        {
            return;
        }

        var documentManager = _serviceProvider.GetKeyedService<ISearchDocumentManager>(indexProfile.ProviderName);

        if (documentManager is null)
        {
            _logger.LogWarning("Skipping AI memory index cleanup because provider '{ProviderName}' is not configured for search indexing.", indexProfile.ProviderName);
            return;
        }

        await documentManager.DeleteAsync(indexProfile, ids, cancellationToken);
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var indexProfile = await GetConfiguredIndexProfileAsync(cancellationToken);

        if (indexProfile is null)
        {
            return;
        }

        var memories = await _memoryStore.GetAllAsync();

        foreach (var memory in memories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await IndexAsync(memory, cancellationToken);
        }
    }

    private async Task<SearchIndexProfile> GetConfiguredIndexProfileAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_memoryOptions.IndexProfileName))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var indexProfile = await _indexProfileStore.FindByNameAsync(_memoryOptions.IndexProfileName);

        if (indexProfile is null)
        {
            _logger.LogWarning("AI memory indexing is configured to use '{IndexProfileName}', but that index profile was not found.", _memoryOptions.IndexProfileName);
            return null;
        }

        if (!string.Equals(indexProfile.Type, IndexProfileTypes.AIMemory, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("AI memory indexing requires an '{ExpectedType}' index profile, but '{IndexProfileName}' is '{ActualType}'.", IndexProfileTypes.AIMemory, _memoryOptions.IndexProfileName, indexProfile.Type);
            return null;
        }

        return indexProfile;
    }

    private async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(SearchIndexProfile indexProfile)
    {
        return await EmbeddingDeploymentResolver.CreateEmbeddingGeneratorAsync(
            _deploymentManager,
            _aiClientFactory,
            SearchIndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile));
    }

    private static IReadOnlyCollection<SearchIndexField> BuildFields(int vectorDimensions)
    {
        return
        [
            new SearchIndexField
            {
                Name = MemoryConstants.ColumnNames.MemoryId,
                FieldType = SearchFieldType.Keyword,
                IsKey = true,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = MemoryConstants.ColumnNames.UserId,
                FieldType = SearchFieldType.Keyword,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = MemoryConstants.ColumnNames.Name,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = MemoryConstants.ColumnNames.Description,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
            },
            new SearchIndexField
            {
                Name = MemoryConstants.ColumnNames.Content,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
            },
            new SearchIndexField
            {
                Name = MemoryConstants.ColumnNames.UpdatedUtc,
                FieldType = SearchFieldType.DateTime,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = MemoryConstants.ColumnNames.Embedding,
                FieldType = SearchFieldType.Vector,
                VectorDimensions = vectorDimensions,
            },
        ];
    }
}
