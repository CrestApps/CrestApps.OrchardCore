using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Services;

public sealed class OrchardAIChatDocumentEventHandler : IAIChatDocumentEventHandler
{
    private readonly List<AIChatUploadedDocument> _uploadedDocuments = [];
    private readonly List<string> _removedChunkIds = [];
    private bool _taskAdded;

    public Task UploadedAsync(AIChatDocumentUploadContext context, CancellationToken cancellationToken = default)
    {
        if (context.UploadedDocuments.Count == 0)
        {
            return Task.CompletedTask;
        }

        AddDeferredTask();
        _uploadedDocuments.AddRange(context.UploadedDocuments);

        return Task.CompletedTask;
    }

    public Task RemovedAsync(AIChatDocumentRemoveContext context, CancellationToken cancellationToken = default)
    {
        if (context.ChunkIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        AddDeferredTask();
        _removedChunkIds.AddRange(context.ChunkIds);

        return Task.CompletedTask;
    }

    private void AddDeferredTask()
    {
        if (_taskAdded)
        {
            return;
        }

        _taskAdded = true;

        var uploadedDocuments = _uploadedDocuments;
        var removedChunkIds = _removedChunkIds;

        ShellScope.AddDeferredTask(scope => ProcessAsync(scope, uploadedDocuments, removedChunkIds));
    }

    private static async Task ProcessAsync(ShellScope scope, List<AIChatUploadedDocument> uploadedDocuments, List<string> removedChunkIds)
    {
        if (uploadedDocuments.Count == 0 && removedChunkIds.Count == 0)
        {
            return;
        }

        var services = scope.ServiceProvider;
        var indexProfileStore = services.GetRequiredService<IIndexProfileStore>();
        var indexProfiles = await indexProfileStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        var documentIndexHandlers = services.GetServices<IDocumentIndexHandler>();
        var logger = services.GetRequiredService<ILogger<OrchardAIChatDocumentEventHandler>>();

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = services.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                continue;
            }

            try
            {
                if (uploadedDocuments.Count > 0)
                {
                    var chunkDocuments = new List<DocumentIndex>();

                    foreach (var uploadedDocument in uploadedDocuments)
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

                            await documentIndexHandlers.InvokeAsync((handler, currentContext) => handler.BuildIndexAsync(currentContext), buildContext, logger);
                            chunkDocuments.Add(documentIndex);
                        }
                    }

                    if (chunkDocuments.Count > 0)
                    {
                        await documentIndexManager.AddOrUpdateDocumentsAsync(indexProfile, chunkDocuments);
                    }
                }

                if (removedChunkIds.Count > 0)
                {
                    await documentIndexManager.DeleteDocumentsAsync(indexProfile, removedChunkIds);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error syncing chat documents with index '{IndexName}'.", indexProfile.IndexName);
            }
        }
    }
}
