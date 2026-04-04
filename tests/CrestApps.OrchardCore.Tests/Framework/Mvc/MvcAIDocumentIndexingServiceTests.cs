using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using CrestApps.Mvc.Web.Areas.Indexing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class MvcAIDocumentIndexingServiceTests
{
    [Fact]
    public async Task IndexAsync_WhenDocumentManagerThrows_ShouldNotThrow()
    {
        var indexProfileStore = new Mock<ISearchIndexProfileStore>();
        indexProfileStore
            .Setup(store => store.FindByNameAsync("chat-documents"))
            .ReturnsAsync(new SearchIndexProfile
            {
                Name = "chat-documents",
                ProviderName = CrestApps.Azure.AISearch.ServiceCollectionExtensions.ProviderName,
                Type = IndexProfileTypes.AIDocuments,
                IndexFullName = "chat-documents-index",
            });

        var indexManager = new Mock<ISearchIndexManager>();
        indexManager
            .Setup(manager => manager.ExistsAsync(It.IsAny<IIndexProfileInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var documentManager = new Mock<ISearchDocumentManager>();
        documentManager
            .Setup(manager => manager.AddOrUpdateAsync(It.IsAny<IIndexProfileInfo>(), It.IsAny<IReadOnlyCollection<IndexDocument>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Index backend unavailable."));

        var services = new ServiceCollection();
        services.AddKeyedSingleton(CrestApps.Azure.AISearch.ServiceCollectionExtensions.ProviderName, indexManager.Object);
        services.AddKeyedSingleton(CrestApps.Azure.AISearch.ServiceCollectionExtensions.ProviderName, documentManager.Object);

        var serviceProvider = services.BuildServiceProvider();

        var service = new MvcAIDocumentIndexingService(
            Options.Create(new InteractionDocumentOptions { IndexProfileName = "chat-documents" }),
            indexProfileStore.Object,
            serviceProvider,
            NullLogger<MvcAIDocumentIndexingService>.Instance);

        var document = new AIDocument
        {
            ItemId = "document-1",
            FileName = "story.pdf",
        };

        IReadOnlyCollection<AIDocumentChunk> chunks =
        [
            new AIDocumentChunk
            {
                ItemId = "chunk-1",
                AIDocumentId = "document-1",
                ReferenceId = "interaction-1",
                ReferenceType = AIReferenceTypes.Document.ChatInteraction,
                Content = "Car race story",
                Embedding = [0.1f, 0.2f],
                Index = 0,
            },
        ];

        await service.IndexAsync(document, chunks, TestContext.Current.CancellationToken);

        documentManager.Verify(
            manager => manager.AddOrUpdateAsync(It.IsAny<IIndexProfileInfo>(), It.IsAny<IReadOnlyCollection<IndexDocument>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
