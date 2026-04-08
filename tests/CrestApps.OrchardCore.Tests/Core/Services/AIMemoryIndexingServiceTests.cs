using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class AIMemoryIndexingServiceTests
{
    [Fact]
    public async Task IndexAsync_WhenConfigured_CreatesIndexAndAddsDocument()
    {
        var indexProfileStore = new Mock<ISearchIndexProfileStore>();
        indexProfileStore
            .Setup(store => store.FindByNameAsync("memory-profile"))
            .ReturnsAsync(new SearchIndexProfile
            {
                Name = "memory-profile",
                ProviderName = "AzureAISearch",
                Type = IndexProfileTypes.AIMemory,
                EmbeddingDeploymentId = "deployment-1",
                IndexFullName = "memory-profile-index",
            });

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(manager => manager.FindByIdAsync("deployment-1"))
            .ReturnsAsync(new AIDeployment
            {
                ItemId = "deployment-1",
                ClientName = "AzureOpenAI",
                ConnectionName = "Default",
                ModelName = "text-embedding-3-small",
            });

        var aiClientFactory = new Mock<IAIClientFactory>();
        aiClientFactory
            .Setup(factory => factory.CreateEmbeddingGeneratorAsync("AzureOpenAI", "Default", "text-embedding-3-small"))
            .Returns(new ValueTask<IEmbeddingGenerator<string, Embedding<float>>>(new FakeEmbeddingGenerator([0.1f, 0.2f, 0.3f])));

        var indexManager = new Mock<ISearchIndexManager>();
        indexManager
            .Setup(manager => manager.ExistsAsync(It.IsAny<IIndexProfileInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        IReadOnlyCollection<IndexDocument> indexedDocuments = null;
        var documentManager = new Mock<ISearchDocumentManager>();
        documentManager
            .Setup(manager => manager.AddOrUpdateAsync(It.IsAny<IIndexProfileInfo>(), It.IsAny<IReadOnlyCollection<IndexDocument>>(), It.IsAny<CancellationToken>()))
            .Callback<IIndexProfileInfo, IReadOnlyCollection<IndexDocument>, CancellationToken>((_, documents, _) => indexedDocuments = documents)
            .ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddKeyedSingleton("AzureAISearch", indexManager.Object);
        services.AddKeyedSingleton("AzureAISearch", documentManager.Object);

        var service = new AIMemoryIndexingService(
            Mock.Of<IAIMemoryStore>(),
            Options.Create(new AIMemoryOptions { IndexProfileName = "memory-profile" }),
            indexProfileStore.Object,
            deploymentManager.Object,
            aiClientFactory.Object,
            services.BuildServiceProvider(),
            NullLogger<AIMemoryIndexingService>.Instance);

        await service.IndexAsync(new AIMemoryEntry
        {
            ItemId = "memory-1",
            UserId = "user-1",
            Name = "name",
            Description = "The user's name.",
            Content = "Mike",
            UpdatedUtc = new DateTime(2026, 4, 5, 20, 0, 0, DateTimeKind.Utc),
        }, TestContext.Current.CancellationToken);

        indexManager.Verify(manager => manager.CreateAsync(
            It.IsAny<IIndexProfileInfo>(),
            It.Is<IReadOnlyCollection<SearchIndexField>>(fields => fields.Any(field =>
                field.Name == MemoryConstants.ColumnNames.Embedding &&
                field.FieldType == SearchFieldType.Vector &&
                field.VectorDimensions == 3)),
            It.IsAny<CancellationToken>()), Times.Once);
        var document = Assert.Single(indexedDocuments);
        Assert.Equal("memory-1", document.Id);
        Assert.Equal("user-1", document.Fields[MemoryConstants.ColumnNames.UserId]);
        Assert.Equal("Mike", document.Fields[MemoryConstants.ColumnNames.Content]);
    }

    [Fact]
    public async Task DeleteAsync_WhenConfigured_RemovesMemoryDocuments()
    {
        var indexProfileStore = new Mock<ISearchIndexProfileStore>();
        indexProfileStore
            .Setup(store => store.FindByNameAsync("memory-profile"))
            .ReturnsAsync(new SearchIndexProfile
            {
                Name = "memory-profile",
                ProviderName = "AzureAISearch",
                Type = IndexProfileTypes.AIMemory,
                IndexFullName = "memory-profile-index",
            });

        IEnumerable<string> deletedIds = null;
        var documentManager = new Mock<ISearchDocumentManager>();
        documentManager
            .Setup(manager => manager.DeleteAsync(It.IsAny<IIndexProfileInfo>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IIndexProfileInfo, IEnumerable<string>, CancellationToken>((_, ids, _) => deletedIds = ids.ToArray())
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddKeyedSingleton("AzureAISearch", documentManager.Object);

        var service = new AIMemoryIndexingService(
            Mock.Of<IAIMemoryStore>(),
            Options.Create(new AIMemoryOptions { IndexProfileName = "memory-profile" }),
            indexProfileStore.Object,
            Mock.Of<IAIDeploymentManager>(),
            Mock.Of<IAIClientFactory>(),
            services.BuildServiceProvider(),
            NullLogger<AIMemoryIndexingService>.Instance);

        await service.DeleteAsync(["memory-1", "memory-1", "", "memory-2"], TestContext.Current.CancellationToken);

        Assert.Equal(["memory-1", "memory-2"], deletedIds);
    }

    private sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly float[] _vector;

        public FakeEmbeddingGenerator(float[] vector)
        {
            _vector = vector;
        }

        public EmbeddingGeneratorMetadata Metadata { get; } = new("fake");

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var embeddings = new GeneratedEmbeddings<Embedding<float>>();

            foreach (var _ in values)
            {
                embeddings.Add(new Embedding<float>(_vector));
            }

            return Task.FromResult(embeddings);
        }

        public object GetService(Type serviceType, object serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
