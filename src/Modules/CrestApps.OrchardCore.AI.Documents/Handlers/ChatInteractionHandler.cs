using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

public sealed class ChatInteractionHandler : CatalogEntryHandlerBase<ChatInteraction>
{
    private readonly Dictionary<string, ChatInteraction> _interactions = [];
    private readonly Dictionary<string, ChatInteraction> _removedInteractions = [];

    public override Task CreatedAsync(CreatedContext<ChatInteraction> context)
    {
        return AddTranscriptAsync(context.Model);
    }

    public override Task UpdatedAsync(UpdatedContext<ChatInteraction> context)
    {
        return AddTranscriptAsync(context.Model);
    }

    public override Task DeletedAsync(DeletedContext<ChatInteraction> context)
        => RemovedTranscriptAsync(context.Model);

    private Task AddTranscriptAsync(ChatInteraction interaction)
    {
        AddDeferredTask();

        _interactions[interaction.ItemId] = interaction;

        return Task.CompletedTask;
    }

    private Task RemovedTranscriptAsync(ChatInteraction interaction)
    {
        AddDeferredTask();

        // Store the last content item in the request.
        _removedInteractions[interaction.ItemId] = interaction;

        return Task.CompletedTask;
    }

    private bool _taskAdded;

    private void AddDeferredTask()
    {
        if (_taskAdded)
        {
            return;
        }

        _taskAdded = true;

        // Using a local variable prevents the lambda from holding a ref on this scoped service.
        var interactions = _interactions;
        var removedTranscripts = _removedInteractions;

        ShellScope.AddDeferredTask(scope => IndexAsync(scope, interactions.Values, removedTranscripts.Values));
    }

    private static async Task IndexAsync(ShellScope scope, IEnumerable<ChatInteraction> interactions, IEnumerable<ChatInteraction> removedInteractions)
    {
        if (!interactions.Any() && !removedInteractions.Any())
        {
            return;
        }

        var services = scope.ServiceProvider;

        var indexStore = services.GetRequiredService<IIndexProfileStore>();

        var indexProfiles = await indexStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        var documentStore = services.GetRequiredService<IAIDocumentStore>();
        var documentIndexHandlers = services.GetRequiredService<IEnumerable<IDocumentIndexHandler>>();
        var logger = services.GetRequiredService<ILogger<ChatInteractionHandler>>();

        // Collect AIDocument records to delete from the store after all index profiles are processed.
        var documentsToDelete = new Dictionary<string, AIDocument>();

        // Collect chunk IDs for removed interactions once (shared across all index profiles).
        var removedChunkIds = new List<string>();

        foreach (var interaction in removedInteractions)
        {
            var removedDocs = await documentStore.GetDocumentsAsync(
                interaction.ItemId,
                AIConstants.DocumentReferenceTypes.ChatInteraction);

            foreach (var doc in removedDocs)
            {
                documentsToDelete.TryAdd(doc.ItemId, doc);

                if (doc.Chunks != null)
                {
                    for (var i = 0; i < doc.Chunks.Count; i++)
                    {
                        removedChunkIds.Add($"{doc.ItemId}_{i}");
                    }
                }
            }
        }

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = services.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                logger.LogWarning("No document index manager found for provider '{ProviderName}'.", indexProfile.ProviderName);

                continue;
            }

            // Collect old chunk IDs for deletion before re-indexing.
            var oldChunkIds = new List<string>();

            foreach (var interaction in interactions)
            {
                var existingDocs = await documentStore.GetDocumentsAsync(
                    interaction.ItemId,
                    AIConstants.DocumentReferenceTypes.ChatInteraction);

                foreach (var doc in existingDocs)
                {
                    if (doc.Chunks != null)
                    {
                        for (var i = 0; i < doc.Chunks.Count; i++)
                        {
                            oldChunkIds.Add($"{doc.ItemId}_{i}");
                        }
                    }
                }
            }

            // Delete old chunk documents.
            if (oldChunkIds.Count > 0)
            {
                await documentIndexManager.DeleteDocumentsAsync(indexProfile, oldChunkIds);
            }

            // Build new flat chunk documents via handler pipeline.
            var documents = new List<DocumentIndex>();

            foreach (var interaction in interactions)
            {
                var aiDocuments = await documentStore.GetDocumentsAsync(
                    interaction.ItemId,
                    AIConstants.DocumentReferenceTypes.ChatInteraction);

                foreach (var aiDocument in aiDocuments)
                {
                    if (aiDocument.Chunks == null || aiDocument.Chunks.Count == 0)
                    {
                        continue;
                    }

                    foreach (var chunk in aiDocument.Chunks)
                    {
                        var chunkId = $"{aiDocument.ItemId}_{chunk.Index}";
                        var documentIndex = new DocumentIndex(chunkId);

                        var aiDocumentChunk = new AIDocumentChunk
                        {
                            ChunkId = chunkId,
                            DocumentId = aiDocument.ItemId,
                            Content = chunk.Text,
                            FileName = aiDocument.FileName,
                            ReferenceId = aiDocument.ReferenceId,
                            ReferenceType = aiDocument.ReferenceType,
                            ChunkIndex = chunk.Index,
                            Embedding = chunk.Embedding,
                        };

                        var buildContext = new BuildDocumentIndexContext(documentIndex, aiDocumentChunk, [chunkId], documentIndexManager.GetContentIndexSettings())
                        {
                            AdditionalProperties = new Dictionary<string, object>
                            {
                                { nameof(IndexProfile), indexProfile },
                            }
                        };

                        await documentIndexHandlers.InvokeAsync((handler, ctx) => handler.BuildIndexAsync(ctx), buildContext, logger);

                        documents.Add(documentIndex);
                    }
                }
            }

            if (documents.Count > 0)
            {
                await documentIndexManager.AddOrUpdateDocumentsAsync(indexProfile, documents);
            }

            // Delete removed interaction chunks from this index profile.
            if (removedChunkIds.Count > 0)
            {
                await documentIndexManager.DeleteDocumentsAsync(indexProfile, removedChunkIds);
            }
        }

        // Delete the AIDocument records from the store after all index profiles have been processed.
        foreach (var doc in documentsToDelete.Values)
        {
            await documentStore.DeleteAsync(doc);
        }
    }
}
