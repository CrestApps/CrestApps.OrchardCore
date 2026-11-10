using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

/// <summary>
/// An <see cref="IAIChatSessionHandler"/> that removes everything a chat session holds when it is deleted:
/// its documents, their chunks, the files stored for them in the document file store (so no blob is
/// orphaned on a remote backend such as Azure Blob Storage), and the working state its features keep.
/// It also schedules deferred removal of the chunks from all AI document indexes.
/// </summary>
public sealed class AIChatSessionDocumentCleanupHandler : AIChatSessionHandlerBase
{
    private readonly IAIDocumentStore _documentStore;
    private readonly IAIDocumentChunkStore _chunkStore;
    private readonly IConversationDocumentCleanupService _cleanupService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionDocumentCleanupHandler"/> class.
    /// </summary>
    /// <param name="documentStore">The document store.</param>
    /// <param name="chunkStore">The chunk store.</param>
    /// <param name="cleanupService">The service that removes a conversation's documents and stored files.</param>
    public AIChatSessionDocumentCleanupHandler(
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore,
        IConversationDocumentCleanupService cleanupService)
    {
        _documentStore = documentStore;
        _chunkStore = chunkStore;
        _cleanupService = cleanupService;
    }

    public override async Task DeletingAsync(
        DeletingContext<AIChatSession> context,
        CancellationToken cancellationToken = default)
    {
        var session = context.Model;

        var documents = await _documentStore.GetDocumentsAsync(
            session.SessionId,
            AIConstants.DocumentReferenceTypes.ChatSession);

        var chunkIds = new List<string>();

        foreach (var doc in documents)
        {
            var chunks = await _chunkStore.GetChunksByAIDocumentIdAsync(doc.ItemId);

            foreach (var chunk in chunks)
            {
                chunkIds.Add(chunk.ItemId);
            }
        }

        // The chunk ids are read above because the cleanup service deletes the chunks. It also deletes the
        // stored file of every document, which removing the rows alone leaves behind as an orphaned blob.
        // It runs even when no document is left, since the session's workspaces outlive the document rows.
        await _cleanupService.CleanupAsync(session.SessionId, AIConstants.DocumentReferenceTypes.ChatSession, cancellationToken);

        if (chunkIds.Count > 0)
        {
            DeferredWork.Add("ai_document_chunk_removal", scope => RemoveDocumentChunksAsync(scope, chunkIds));
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
