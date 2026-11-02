using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Documents.Services;
using CrestApps.Core.AI.Documents.Tabular;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.AI.Documents;

public sealed class AIChatSessionDocumentCleanupHandlerTests
{
    private const string SessionId = "session-1";

    [Fact]
    public async Task DeletingAsync_RunsTheCleanupServiceForTheSession()
    {
        var documentStore = new Mock<IAIDocumentStore>();
        documentStore
            .Setup(store => store.GetDocumentsAsync(SessionId, AIConstants.DocumentReferenceTypes.ChatSession))
            .ReturnsAsync([]);

        var cleanupService = new Mock<IConversationDocumentCleanupService>();

        var handler = new AIChatSessionDocumentCleanupHandler(
            documentStore.Object,
            Mock.Of<IAIDocumentChunkStore>(),
            cleanupService.Object);

        await handler.DeletingAsync(new DeletingContext<AIChatSession>(new AIChatSession { SessionId = SessionId }));

        // Runs even for a session with no documents because the workspaces it keeps outlive the document rows.
        cleanupService.Verify(
            service => service.CleanupAsync(SessionId, AIConstants.DocumentReferenceTypes.ChatSession, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeletingAsync_DeletesTheStoredFileOfEverySessionDocument()
    {
        const string StoredFilePath = "documents/session-1/report.pdf";

        var document = new AIDocument
        {
            ItemId = "document-1",
            ReferenceId = SessionId,
            ReferenceType = AIConstants.DocumentReferenceTypes.ChatSession,
            StoredFilePath = StoredFilePath,
        };

        var documentStore = new Mock<IAIDocumentStore>();
        documentStore
            .Setup(store => store.GetDocumentsAsync(SessionId, AIConstants.DocumentReferenceTypes.ChatSession))
            .ReturnsAsync([document]);

        var chunkStore = new Mock<IAIDocumentChunkStore>();
        chunkStore
            .Setup(store => store.GetChunksByAIDocumentIdAsync(document.ItemId))
            .ReturnsAsync([]);

        var fileStore = new Mock<IDocumentFileStore>();
        fileStore
            .Setup(store => store.DeleteFileAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        var cleanupService = new DefaultConversationDocumentCleanupService(
            documentStore.Object,
            chunkStore.Object,
            fileStore.Object,
            Mock.Of<ITabularDocumentArtifactStore>(),
            Options.Create(new DocumentFileSystemFileStoreOptions()),
            NullLogger<DefaultConversationDocumentCleanupService>.Instance);

        var handler = new AIChatSessionDocumentCleanupHandler(documentStore.Object, chunkStore.Object, cleanupService);

        await handler.DeletingAsync(new DeletingContext<AIChatSession>(new AIChatSession { SessionId = SessionId }));

        fileStore.Verify(store => store.DeleteFileAsync(StoredFilePath), Times.Once);
        chunkStore.Verify(store => store.DeleteByDocumentIdAsync(document.ItemId), Times.Once);
        documentStore.Verify(store => store.DeleteAsync(document, It.IsAny<CancellationToken>()), Times.Once);
    }
}
