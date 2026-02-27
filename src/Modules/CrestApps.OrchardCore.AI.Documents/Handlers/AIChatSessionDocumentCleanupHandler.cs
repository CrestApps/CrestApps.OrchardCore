using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
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
    private readonly ILogger _logger;

    public AIChatSessionDocumentCleanupHandler(
        IAIDocumentStore documentStore,
        ILogger<AIChatSessionDocumentCleanupHandler> logger)
    {
        _documentStore = documentStore;
        _logger = logger;
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
            if (doc.Chunks != null)
            {
                for (var i = 0; i < doc.Chunks.Count; i++)
                {
                    chunkIds.Add($"{doc.ItemId}_{i}");
                }
            }

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
