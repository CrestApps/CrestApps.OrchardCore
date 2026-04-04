using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Areas.AIChat.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class MvcAIChatDocumentEventHandlerTests
{
    [Fact]
    public async Task UploadedAsync_ShouldQueueEachUploadedDocument()
    {
        var queue = new Mock<IMvcAIChatDocumentIndexingQueue>();
        queue
            .Setup(service => service.QueueIndexAsync(It.IsAny<AIDocument>(), It.IsAny<IReadOnlyCollection<AIDocumentChunk>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var handler = new MvcAIChatDocumentEventHandler(queue.Object);

        await handler.UploadedAsync(new AIChatDocumentUploadContext
        {
            UploadedDocuments =
            [
                new AIChatUploadedDocument
                {
                    Document = new AIDocument { ItemId = "doc-1" },
                    Chunks = [new AIDocumentChunk { ItemId = "chunk-1" }],
                },
                new AIChatUploadedDocument
                {
                    Document = new AIDocument { ItemId = "doc-2" },
                    Chunks = [new AIDocumentChunk { ItemId = "chunk-2" }],
                },
            ],
        }, TestContext.Current.CancellationToken);

        queue.Verify(service => service.QueueIndexAsync(It.IsAny<AIDocument>(), It.IsAny<IReadOnlyCollection<AIDocumentChunk>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RemovedAsync_WhenChunkIdsExist_ShouldQueueDelete()
    {
        var queue = new Mock<IMvcAIChatDocumentIndexingQueue>();
        queue
            .Setup(service => service.QueueDeleteChunksAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var handler = new MvcAIChatDocumentEventHandler(queue.Object);

        await handler.RemovedAsync(new AIChatDocumentRemoveContext
        {
            ChunkIds = ["chunk-1", "chunk-2"],
        }, TestContext.Current.CancellationToken);

        queue.Verify(service => service.QueueDeleteChunksAsync(It.Is<IReadOnlyCollection<string>>(ids => ids.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
    }
}
