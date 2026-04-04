using CrestApps.AI.Models;

namespace CrestApps.Mvc.Web.Areas.AIChat.Services;

public interface IMvcAIChatDocumentIndexingQueue
{
    ValueTask QueueIndexAsync(AIDocument document, IReadOnlyCollection<AIDocumentChunk> chunks, CancellationToken cancellationToken = default);

    ValueTask QueueDeleteChunksAsync(IReadOnlyCollection<string> chunkIds, CancellationToken cancellationToken = default);
}
