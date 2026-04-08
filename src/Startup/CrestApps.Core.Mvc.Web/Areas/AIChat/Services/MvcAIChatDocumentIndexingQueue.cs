using System.Threading.Channels;
using CrestApps.Core.AI.Models;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Services;

public sealed class MvcAIChatDocumentIndexingQueue : IMvcAIChatDocumentIndexingQueue
{
    private readonly Channel<MvcAIChatDocumentIndexingWorkItem> _channel = Channel.CreateUnbounded<MvcAIChatDocumentIndexingWorkItem>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ValueTask QueueIndexAsync(AIDocument document, IReadOnlyCollection<AIDocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(chunks);

        return _channel.Writer.WriteAsync(
            MvcAIChatDocumentIndexingWorkItem.ForIndex(
                document,
                chunks.ToArray()),
            cancellationToken);
    }

    public ValueTask QueueDeleteChunksAsync(IReadOnlyCollection<string> chunkIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunkIds);

        return _channel.Writer.WriteAsync(
            MvcAIChatDocumentIndexingWorkItem.ForDeleteChunks(
                chunkIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray()),
            cancellationToken);
    }

    internal IAsyncEnumerable<MvcAIChatDocumentIndexingWorkItem> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

internal sealed class MvcAIChatDocumentIndexingWorkItem
{
    public AIDocument Document { get; private init; }
    public IReadOnlyCollection<AIDocumentChunk> Chunks { get; private init; } = [];
    public IReadOnlyCollection<string> ChunkIds { get; private init; } = [];
    public MvcAIChatDocumentIndexingWorkItemType Type { get; private init; }

    public static MvcAIChatDocumentIndexingWorkItem ForIndex(AIDocument document, IReadOnlyCollection<AIDocumentChunk> chunks) =>
        new()
        {
            Document = document,
            Chunks = chunks,
            Type = MvcAIChatDocumentIndexingWorkItemType.Index,
        };

    public static MvcAIChatDocumentIndexingWorkItem ForDeleteChunks(IReadOnlyCollection<string> chunkIds) =>
        new()
        {
            ChunkIds = chunkIds,
            Type = MvcAIChatDocumentIndexingWorkItemType.DeleteChunks,
        };
}

internal enum MvcAIChatDocumentIndexingWorkItemType
{
    Index,
    DeleteChunks,
}
