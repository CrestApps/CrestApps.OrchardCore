using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

/// <summary>
/// An <see cref="IAIChatSessionHandler"/> that removes all documents associated
/// with a chat session from the document store and schedules deferred removal
/// of their chunks from all AI document indexes when the session is deleted.
/// </summary>
public sealed class AIChatSessionDocumentCleanupHandler : AIChatSessionHandlerBase
{
    private readonly IAIDocumentStore _documentStore;
    private readonly IAIDocumentChunkStore _chunkStore;

    public AIChatSessionDocumentCleanupHandler(
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore)
    {
        _documentStore = documentStore;
        _chunkStore = chunkStore;
    }

    public override async Task DeletingAsync(DeletingContext<AIChatSession> context)
    {
        var session = context.Model;

        var documents = await _documentStore.GetDocumentsAsync(
            session.SessionId,
            AIConstants.DocumentReferenceTypes.ChatSession);

        if (documents.Count == 0)
        {
            return;
        }

        var chunkIds = new List<string>();

        foreach (var doc in documents)
        {
            var chunks = await _chunkStore.GetChunksByAIDocumentIdAsync(doc.ItemId);

            foreach (var chunk in chunks)
            {
                chunkIds.Add(chunk.ItemId);
            }

            await _chunkStore.DeleteByDocumentIdAsync(doc.ItemId);
            await _documentStore.DeleteAsync(doc);
        }

        if (chunkIds.Count > 0)
        {
            ShellScope.AddDeferredTask(scope => RemoveDocumentChunksAsync(scope, chunkIds));
        }
    }

    private static async Task RemoveDocumentChunksAsync(ShellScope scope, List<string> chunkIds)
    {
        var services = scope.ServiceProvider;
        var indexStore = services.GetRequiredService<IIndexProfileStore>();

        var indexProfiles = await indexStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        var logger = services.GetRequiredService<ILogger<AIChatSessionDocumentCleanupHandler>>();

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = services.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                continue;
            }

            try
            {
                await documentIndexManager.DeleteDocumentsAsync(indexProfile, chunkIds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error removing session document chunks from index '{IndexName}'.", indexProfile.IndexName);
            }
        }
    }
}
