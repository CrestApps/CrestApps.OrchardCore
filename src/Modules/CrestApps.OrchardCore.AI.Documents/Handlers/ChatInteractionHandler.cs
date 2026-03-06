using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
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
        var chunkStore = services.GetRequiredService<IAIDocumentChunkStore>();
        var documentIndexHandlers = services.GetRequiredService<IEnumerable<IDocumentIndexHandler>>();
        var logger = services.GetRequiredService<ILogger<ChatInteractionHandler>>();
        var aiClientFactory = services.GetRequiredService<IAIClientFactory>();

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

                var chunks = await chunkStore.GetChunksByAIDocumentIdAsync(doc.ItemId);
                foreach (var chunk in chunks)
                {
                    removedChunkIds.Add(chunk.ItemId);
                }

                await chunkStore.DeleteByDocumentIdAsync(doc.ItemId);
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

            // Resolve embedding generator from the index profile metadata.
            var metadata = indexProfile.As<CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models.ChatInteractionIndexProfileMetadata>();
            Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>> embeddingGenerator = null;

            if (!string.IsNullOrEmpty(metadata?.EmbeddingProviderName) &&
                !string.IsNullOrEmpty(metadata?.EmbeddingConnectionName))
            {
                embeddingGenerator = await aiClientFactory.CreateEmbeddingGeneratorAsync(
                    metadata.EmbeddingProviderName,
                    metadata.EmbeddingConnectionName,
                    metadata.EmbeddingDeploymentName);
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
                    var chunks = await chunkStore.GetChunksByAIDocumentIdAsync(doc.ItemId);
                    foreach (var chunk in chunks)
                    {
                        oldChunkIds.Add(chunk.ItemId);
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
                    var docChunks = await chunkStore.GetChunksByAIDocumentIdAsync(aiDocument.ItemId);

                    if (docChunks.Count == 0)
                    {
                        continue;
                    }

                    // Generate embeddings for all chunk texts in batch.
                    var chunkTexts = docChunks.Select(c => c.Content).ToList();
                    Microsoft.Extensions.AI.GeneratedEmbeddings<Microsoft.Extensions.AI.Embedding<float>> embeddings = null;

                    if (embeddingGenerator != null && chunkTexts.Count > 0)
                    {
                        try
                        {
                            embeddings = await embeddingGenerator.GenerateAsync(chunkTexts);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to generate embeddings for document {DocumentId}.", aiDocument.ItemId);
                        }
                    }

                    var chunkList = docChunks.ToList();

                    for (var i = 0; i < chunkList.Count; i++)
                    {
                        var chunk = chunkList[i];
                        var documentIndex = new DocumentIndex(chunk.ItemId);

                        var aiDocumentChunk = new AIDocumentChunkContext
                        {
                            ChunkId = chunk.ItemId,
                            DocumentId = aiDocument.ItemId,
                            Content = chunk.Content,
                            FileName = aiDocument.FileName,
                            ReferenceId = aiDocument.ReferenceId,
                            ReferenceType = aiDocument.ReferenceType,
                            ChunkIndex = chunk.Index,
                            Embedding = embeddings != null && i < embeddings.Count ? embeddings[i].Vector.ToArray() : null,
                        };

                        var buildContext = new BuildDocumentIndexContext(documentIndex, aiDocumentChunk, [chunk.ItemId], documentIndexManager.GetContentIndexSettings())
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
