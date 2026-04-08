using CrestApps.Core.AI;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Services;

public sealed class MvcAIChatDocumentEventHandler : IAIChatDocumentEventHandler
{
    private readonly IMvcAIChatDocumentIndexingQueue _indexingQueue;

    public MvcAIChatDocumentEventHandler(IMvcAIChatDocumentIndexingQueue indexingQueue)
    {
        _indexingQueue = indexingQueue;
    }

    public async Task UploadedAsync(AIChatDocumentUploadContext context, CancellationToken cancellationToken = default)
    {
        foreach (var document in context.UploadedDocuments)
        {
            await _indexingQueue.QueueIndexAsync(document.Document, document.Chunks, cancellationToken);
        }
    }

    public async Task RemovedAsync(AIChatDocumentRemoveContext context, CancellationToken cancellationToken = default)
    {
        if (context.ChunkIds.Count == 0)
        {
            return;
        }

        await _indexingQueue.QueueDeleteChunksAsync(context.ChunkIds, cancellationToken);
    }
}
