using CrestApps.Core.AI.Models;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Services;

public interface IMvcAIChatDocumentIndexingQueue
{
    ValueTask QueueIndexAsync(AIDocument document, IReadOnlyCollection<AIDocumentChunk> chunks, CancellationToken cancellationToken = default);

    ValueTask QueueDeleteChunksAsync(IReadOnlyCollection<string> chunkIds, CancellationToken cancellationToken = default);
}
