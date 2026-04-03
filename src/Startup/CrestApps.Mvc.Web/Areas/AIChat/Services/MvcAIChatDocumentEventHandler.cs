using CrestApps.AI;
using CrestApps.Mvc.Web.Areas.Indexing.Services;

namespace CrestApps.Mvc.Web.Areas.AIChat.Services;

public sealed class MvcAIChatDocumentEventHandler : IAIChatDocumentEventHandler
{
    private readonly MvcAIDocumentIndexingService _documentIndexingService;

    public MvcAIChatDocumentEventHandler(MvcAIDocumentIndexingService documentIndexingService)
    {
        _documentIndexingService = documentIndexingService;
    }

    public async Task UploadedAsync(AIChatDocumentUploadContext context, CancellationToken cancellationToken = default)
    {
        foreach (var document in context.UploadedDocuments)
        {
            await _documentIndexingService.IndexAsync(document.Document, document.Chunks, cancellationToken);
        }
    }

    public async Task RemovedAsync(AIChatDocumentRemoveContext context, CancellationToken cancellationToken = default)
    {
        if (context.ChunkIds.Count == 0)
        {
            return;
        }

        await _documentIndexingService.DeleteChunksAsync(context.ChunkIds, cancellationToken);
    }
}
