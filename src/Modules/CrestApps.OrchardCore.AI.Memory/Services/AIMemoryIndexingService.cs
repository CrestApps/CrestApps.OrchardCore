using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Memory.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Memory.Services;

internal sealed class AIMemoryIndexingService
{
    private readonly IAIMemoryStore _memoryStore;
    private readonly AIMemoryOptions _memoryOptions;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IDocumentIndexHandler> _documentIndexHandlers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIMemoryIndexingService"/> class.
    /// </summary>
    /// <param name="memoryStore">The memory store.</param>
    /// <param name="memoryOptions">The memory options.</param>
    /// <param name="indexProfileStore">The index profile store.</param>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="aiClientFactory">The ai client factory.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="documentIndexHandlers">The document index handlers.</param>
    /// <param name="logger">The logger.</param>
    public AIMemoryIndexingService(
        IAIMemoryStore memoryStore,
        IOptions<AIMemoryOptions> memoryOptions,
        IIndexProfileStore indexProfileStore,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        IServiceProvider serviceProvider,
        IEnumerable<IDocumentIndexHandler> documentIndexHandlers,
        ILogger<AIMemoryIndexingService> logger)
    {
        _memoryStore = memoryStore;
        _memoryOptions = memoryOptions.Value;
        _indexProfileStore = indexProfileStore;
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
        _serviceProvider = serviceProvider;
        _documentIndexHandlers = documentIndexHandlers;
        _logger = logger;
    }

    /// <summary>
    /// Indexes the .
    /// </summary>
    /// <param name="memory">The memory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task IndexAsync(AIMemoryEntry memory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_memoryOptions.IndexProfileName))
        {
            return;
        }

        var indexProfile = await _indexProfileStore.FindByNameAsync(_memoryOptions.IndexProfileName);

        if (indexProfile is null || !string.Equals(indexProfile.Type, MemoryConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await IndexAsync(memory, indexProfile, cancellationToken);
    }

    /// <summary>
    /// Asynchronously performs the sync by index profile ids operation.
    /// </summary>
    /// <param name="indexProfileIds">The index profile ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SyncByIndexProfileIdsAsync(IEnumerable<string> indexProfileIds, CancellationToken cancellationToken = default)
    {
        var ids = indexProfileIds?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (ids is null || ids.Count == 0)
        {
            return;
        }

        var profiles = (await _indexProfileStore.GetByTypeAsync(MemoryConstants.IndexingTaskType))
            .Where(x => ids.Contains(x.Id))
            .ToArray();

        if (profiles.Length == 0)
        {
            return;
        }

        var memories = await _memoryStore.GetAllAsync(cancellationToken);

        foreach (var indexProfile in profiles)
        {
            var documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager is null)
            {
                continue;
            }

            var documents = new List<DocumentIndex>();

            foreach (var memory in memories)
            {
                var document = await BuildDocumentAsync(memory, indexProfile, documentIndexManager, cancellationToken);

                if (document != null)
                {
                    documents.Add(document);
                }
            }

            if (documents.Count == 0)
            {
                continue;
            }

            await documentIndexManager.AddOrUpdateDocumentsAsync(indexProfile, documents);
        }
    }

    /// <summary>
    /// Removes the async.
    /// </summary>
    /// <param name="memoryIds">The memory ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DeleteAsync(IEnumerable<string> memoryIds, CancellationToken cancellationToken = default)
    {
        var ids = memoryIds?
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids is null || ids.Length == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_memoryOptions.IndexProfileName))
        {
            return;
        }

        var indexProfile = await _indexProfileStore.FindByNameAsync(_memoryOptions.IndexProfileName);

        if (indexProfile is null || !string.Equals(indexProfile.Type, MemoryConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

        if (documentIndexManager is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await documentIndexManager.DeleteDocumentsAsync(indexProfile, ids);
    }

    private async Task IndexAsync(AIMemoryEntry memory, IndexProfile indexProfile, CancellationToken cancellationToken)
    {
        var documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

        if (documentIndexManager is null)
        {
            return;
        }

        var document = await BuildDocumentAsync(memory, indexProfile, documentIndexManager, cancellationToken);

        if (document is null)
        {
            return;
        }

        await documentIndexManager.AddOrUpdateDocumentsAsync(indexProfile, [document]);
    }

    private async Task<DocumentIndex> BuildDocumentAsync(
        AIMemoryEntry memory,
        IndexProfile indexProfile,
        IDocumentIndexManager documentIndexManager,
        CancellationToken cancellationToken)
    {
        var embeddingGenerator = await CreateEmbeddingGeneratorAsync(indexProfile);

        if (embeddingGenerator is null)
        {
            return null;
        }

        var embeddingText = $"Name: {memory.Name}{Environment.NewLine}Description: {memory.Description}";

        var embeddings = await embeddingGenerator.GenerateAsync(
            [embeddingText],
            cancellationToken: cancellationToken);

        if (embeddings is null || embeddings.Count == 0 || embeddings[0]?.Vector is null)
        {
            return null;
        }

        var record = new AIMemoryEntryIndexDocument
        {
            MemoryId = memory.ItemId,
            UserId = memory.UserId,
            Name = memory.Name,
            Description = memory.Description,
            Content = memory.Content,
            UpdatedUtc = memory.UpdatedUtc,
            Embedding = embeddings[0].Vector.ToArray(),
        };

        var documentIndex = new DocumentIndex(memory.ItemId);
        var buildContext = new BuildDocumentIndexContext(
            documentIndex,
            record,
            [memory.ItemId],
            documentIndexManager.GetContentIndexSettings())
        {
            AdditionalProperties = new Dictionary<string, object>
            {
                [nameof(IndexProfile)] = indexProfile,
            },
        };

        await _documentIndexHandlers.InvokeAsync((handler, ctx) => handler.BuildIndexAsync(ctx), buildContext, _logger);

        return documentIndex;
    }

    private async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(IndexProfile indexProfile)
    {
        return await EmbeddingDeploymentResolver.CreateEmbeddingGeneratorAsync(
            _deploymentManager,
            _aiClientFactory,
            IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile));
    }
}
