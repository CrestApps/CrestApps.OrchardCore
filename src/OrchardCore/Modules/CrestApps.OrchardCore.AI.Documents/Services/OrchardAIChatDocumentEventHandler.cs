using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Services;

public sealed class OrchardAIChatDocumentEventHandler : IAIChatDocumentEventHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrchardAIChatDocumentEventHandler> _logger;

    public OrchardAIChatDocumentEventHandler(
        IServiceProvider serviceProvider,
        ILogger<OrchardAIChatDocumentEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task UploadedAsync(AIChatDocumentUploadContext context, CancellationToken cancellationToken = default)
    {
        if (context.Session == null || context.UploadedDocuments.Count == 0)
        {
            return;
        }

        var indexProfileStore = _serviceProvider.GetService<IIndexProfileStore>();

        if (indexProfileStore == null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipping Orchard AI chat document indexing because {ServiceName} is not registered.", nameof(IIndexProfileStore));
            }
            return;
        }

        var documentIndexHandlers = _serviceProvider.GetServices<IDocumentIndexHandler>().ToArray();
        var indexProfiles = await indexProfileStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                continue;
            }

            var chunkDocuments = new List<DocumentIndex>();

            foreach (var uploadedDocument in context.UploadedDocuments)
            {
                foreach (var chunk in uploadedDocument.Chunks)
                {
                    var documentIndex = new DocumentIndex(chunk.ItemId);
                    var aiDocumentChunk = new AIDocumentChunkContext
                    {
                        ChunkId = chunk.ItemId,
                        DocumentId = uploadedDocument.Document.ItemId,
                        Content = chunk.Content,
                        FileName = uploadedDocument.Document.FileName,
                        ReferenceId = uploadedDocument.Document.ReferenceId,
                        ReferenceType = uploadedDocument.Document.ReferenceType,
                        ChunkIndex = chunk.Index,
                        Embedding = chunk.Embedding,
                    };

                    var buildContext = new BuildDocumentIndexContext(
                        documentIndex,
                        aiDocumentChunk,
                        [chunk.ItemId],
                        documentIndexManager.GetContentIndexSettings())
                    {
                        AdditionalProperties = new Dictionary<string, object>
                        {
                            { nameof(IndexProfile), indexProfile },
                        },
                    };

                    await documentIndexHandlers.InvokeAsync((handler, currentContext) => handler.BuildIndexAsync(currentContext), buildContext, _logger);
                    chunkDocuments.Add(documentIndex);
                }
            }

            if (chunkDocuments.Count > 0)
            {
                await documentIndexManager.AddOrUpdateDocumentsAsync(indexProfile, chunkDocuments);
            }
        }
    }

    public async Task RemovedAsync(AIChatDocumentRemoveContext context, CancellationToken cancellationToken = default)
    {
        if (context.ChunkIds.Count == 0)
        {
            return;
        }

        var indexProfileStore = _serviceProvider.GetService<IIndexProfileStore>();

        if (indexProfileStore == null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipping Orchard AI chat document index cleanup because {ServiceName} is not registered.", nameof(IIndexProfileStore));
            }
            return;
        }

        var indexProfiles = await indexProfileStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                continue;
            }

            await documentIndexManager.DeleteDocumentsAsync(indexProfile, context.ChunkIds);
        }
    }
}
