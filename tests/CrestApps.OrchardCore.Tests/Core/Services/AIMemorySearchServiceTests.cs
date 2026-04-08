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

public sealed class AIMemorySearchServiceTests
{
    [Fact]
    public async Task SearchAsync_WhenIndexProfileIsNotConfigured_ReturnsEmpty()
    {
        var service = CreateService(options: new AIMemoryOptions());

        var results = await service.SearchAsync("user-1", ["hello"], requestedTopN: null, TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_WhenDependenciesResolve_ReturnsAggregatedResults()
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
            });

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(manager => manager.FindByIdAsync("deployment-1"))
            .ReturnsAsync(new AIDeployment
            {
                ItemId = "deployment-1",
                ClientName = "AzureOpenAI",
                ConnectionName = "Default",
                Name = "text-embedding-3-small",
                ModelName = "text-embedding-3-small",
            });

        var vectorSearchService = new Mock<IMemoryVectorSearchService>();
        vectorSearchService
            .Setup(service => service.SearchAsync(
                It.IsAny<SearchIndexProfile>(),
                It.IsAny<float[]>(),
                "user-1",
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AIMemorySearchResult
                {
                    MemoryId = "memory-1",
                    Name = "project",
                    Content = "CrestApps.OrchardCore",
                    Score = 0.75f,
                },
                new AIMemorySearchResult
                {
                    MemoryId = "memory-1",
                    Name = "project",
                    Content = "CrestApps.OrchardCore",
                    Score = 0.91f,
                },
            ]);

        var aiClientFactory = new Mock<IAIClientFactory>();
        aiClientFactory
            .Setup(factory => factory.CreateEmbeddingGeneratorAsync("AzureOpenAI", "Default", "text-embedding-3-small"))
            .Returns(new ValueTask<IEmbeddingGenerator<string, Embedding<float>>>(new FakeEmbeddingGenerator([1f, 2f, 3f])));

        var service = CreateService(
            options: new AIMemoryOptions
            {
                IndexProfileName = "memory-profile",
                TopN = 2,
            },
            aiClientFactory: aiClientFactory.Object,
            configureServices: services =>
            {
                services.AddSingleton(indexProfileStore.Object);
                services.AddSingleton(deploymentManager.Object);
                services.AddKeyedSingleton<IMemoryVectorSearchService>("AzureAISearch", vectorSearchService.Object);
            });

        var results = (await service.SearchAsync("user-1", ["hello", "hello "], requestedTopN: null, TestContext.Current.CancellationToken)).ToList();

        var result = Assert.Single(results);
        Assert.Equal("memory-1", result.MemoryId);
        Assert.Equal(0.91f, result.Score);
    }

    private static AIMemorySearchService CreateService(
        AIMemoryOptions options,
        IAIClientFactory aiClientFactory = null,
        Action<IServiceCollection> configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(aiClientFactory ?? Mock.Of<IAIClientFactory>());
        services.AddSingleton(Mock.Of<ISearchIndexProfileStore>());
        services.AddSingleton(Mock.Of<IAIDeploymentManager>());
        configureServices?.Invoke(services);

        var serviceProvider = services.BuildServiceProvider();

        return new AIMemorySearchService(
            serviceProvider,
            serviceProvider.GetRequiredService<ISearchIndexProfileStore>(),
            serviceProvider.GetRequiredService<IAIDeploymentManager>(),
            serviceProvider.GetRequiredService<IAIClientFactory>(),
            Options.Create(options),
            NullLogger<AIMemorySearchService>.Instance);
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
