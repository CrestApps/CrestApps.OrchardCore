using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Services;

/// <summary>
/// Adds the chunks of a profile's documents to every AI documents index, so retrieval can find them.
/// </summary>
internal static class ProfileDocumentChunkIndexer
{
    /// <summary>
    /// Schedules the chunks of <paramref name="documents"/> to be indexed once the current shell scope
    /// completes, after the documents and their chunks are committed.
    /// </summary>
    /// <param name="documents">The documents whose chunks to index.</param>
    public static void ScheduleIndexing(IReadOnlyCollection<AIDocument> documents)
    {
        if (documents is not { Count: > 0 })
        {
            return;
        }

        ShellScope.AddDeferredTask(scope => IndexDocumentChunksAsync(scope, documents));
    }

    private static async Task IndexDocumentChunksAsync(ShellScope scope, IReadOnlyCollection<AIDocument> documents)
    {
        var services = scope.ServiceProvider;
        var indexStore = services.GetRequiredService<IIndexProfileStore>();
        var indexProfiles = await indexStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        var chunkStore = services.GetRequiredService<IAIDocumentChunkStore>();
        var documentIndexHandlers = services.GetRequiredService<IEnumerable<IDocumentIndexHandler>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ProfileDocumentChunkIndexer));

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = services.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                continue;
            }

            var chunkDocuments = new List<DocumentIndex>();

            foreach (var aiDocument in documents)
            {
                var chunks = await chunkStore.GetChunksByAIDocumentIdAsync(aiDocument.ItemId);

                if (chunks.Count == 0)
                {
                    continue;
                }

                foreach (var chunk in chunks)
                {
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
                        Embedding = chunk.Embedding,
                    };

                    var buildContext = new BuildDocumentIndexContext(documentIndex, aiDocumentChunk, [chunk.ItemId], documentIndexManager.GetContentIndexSettings())
                    {
                        AdditionalProperties = new Dictionary<string, object>
                        {
                            { nameof(IndexProfile), indexProfile },
                        }
                    };

                    await documentIndexHandlers.InvokeAsync((handler, ctx) => handler.BuildIndexAsync(ctx), buildContext, logger);

                    chunkDocuments.Add(documentIndex);
                }
            }

            if (chunkDocuments.Count > 0)
            {
                await documentIndexManager.AddOrUpdateDocumentsAsync(indexProfile, chunkDocuments);
            }
        }
    }
}
