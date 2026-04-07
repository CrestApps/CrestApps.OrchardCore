using CrestApps.AI;
using CrestApps.AI.Chat;
using CrestApps.AI.Clients;
using CrestApps.AI.Deployments;
using CrestApps.AI.Memory;
using CrestApps.AI.Models;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using CrestApps.Templates.Models;
using CrestApps.Templates.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using CrestApps;

#pragma warning disable MEAI001

namespace CrestApps.OrchardCore.Tests.Core.Chat;

public sealed class DocumentPreemptiveRagHandlerTests
{
    [Fact]
    public async Task HandleAsync_ProfileKnowledgeDocuments_InjectsRetrievedChunksAndReferences()
    {
        var indexProfile = new SearchIndexProfile
        {
            Name = "docs-index",
            ProviderName = "test-provider",
        };
        indexProfile.Put(new DataSourceIndexProfileMetadata
        {
            EmbeddingDeploymentId = "embedding-id",
        });

        var indexProfileStore = new Mock<ISearchIndexProfileStore>();
        indexProfileStore
            .Setup(store => store.FindByNameAsync("docs-index"))
            .ReturnsAsync(indexProfile);

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(manager => manager.FindByIdAsync("embedding-id"))
            .ReturnsAsync(new AIDeployment
            {
                ItemId = "embedding-id",
                Name = "embedding",
                ModelName = "embedding",
                ClientName = "OpenAI",
                ConnectionName = "Default",
                Type = AIDeploymentType.Embedding,
            });

        var vectorSearchService = new Mock<IVectorSearchService>();
        vectorSearchService
            .Setup(service => service.SearchAsync(
                indexProfile,
                It.IsAny<float[]>(),
                "profile-1",
                AIReferenceTypes.Document.Profile,
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DocumentChunkSearchResult
                {
                    DocumentKey = "doc-1",
                    FileName = "race.pdf",
                    Score = 0.95f,
                    Chunk = new ChatInteractionDocumentChunk
                    {
                        Index = 0,
                        Text = "Carla and Mark race their go carts, and Carla wins the race.",
                    },
                },
            ]);

        var services = new ServiceCollection()
            .AddSingleton<IAIClientFactory>(new FakeAIClientFactory(new FakeEmbeddingGenerator([0.1f, 0.2f])))
            .AddSingleton<IAIDeploymentManager>(deploymentManager.Object)
            .AddSingleton<ISearchIndexProfileStore>(indexProfileStore.Object)
            .AddSingleton<ITemplateService, FakeTemplateService>()
            .AddSingleton<IOptions<InteractionDocumentOptions>>(Options.Create(new InteractionDocumentOptions
            {
                IndexProfileName = "docs-index",
                TopN = 3,
            }))
            .AddLogging()
            .AddKeyedSingleton<IVectorSearchService>("test-provider", vectorSearchService.Object)
            .AddDefaultDocumentProcessingServices()
            .BuildServiceProvider();

        var handler = services.GetServices<IPreemptiveRagHandler>().Single();

        var profile = new AIProfile { ItemId = "profile-1" };
        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext(),
            Documents =
            [
                new ChatDocumentInfo
                {
                    DocumentId = "doc-1",
                    FileName = "race.pdf",
                },
            ],
        };

        var builtContext = new OrchestrationContextBuiltContext(profile, context);
        var canHandle = await handler.CanHandleAsync(builtContext);

        Assert.True(canHandle);

        await handler.HandleAsync(new PreemptiveRagContext(context, profile, ["tell me about car race story"]));

        var systemMessage = context.SystemMessageBuilder.ToString();
        Assert.Contains("[Retrieved Document Context]", systemMessage);
        Assert.Contains("Carla and Mark race their go carts", systemMessage);
    }

    [Fact]
    public async Task HandleAsync_NoIndexProfileConfigured_DoesNotModifySystemMessage()
    {
        var services = new ServiceCollection()
            .AddSingleton<IAIClientFactory>(new FakeAIClientFactory(new FakeEmbeddingGenerator([0.1f])))
            .AddSingleton<IAIDeploymentManager>(Mock.Of<IAIDeploymentManager>())
            .AddSingleton<ISearchIndexProfileStore>(Mock.Of<ISearchIndexProfileStore>())
            .AddSingleton<ITemplateService, FakeTemplateService>()
            .AddSingleton<IOptions<InteractionDocumentOptions>>(Options.Create(new InteractionDocumentOptions()))
            .AddLogging()
            .AddDefaultDocumentProcessingServices()
            .BuildServiceProvider();
        var handler = services.GetServices<IPreemptiveRagHandler>().Single();

        var profile = new AIProfile { ItemId = "profile-1" };
        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext(),
            Documents =
            [
                new ChatDocumentInfo
                {
                    DocumentId = "doc-1",
                    FileName = "race.pdf",
                },
            ],
        };

        await handler.HandleAsync(new PreemptiveRagContext(context, profile, ["tell me about car race story"]));

        Assert.Equal(string.Empty, context.SystemMessageBuilder.ToString());
        Assert.False(context.Properties.ContainsKey("DocumentReferences"));
    }

    private sealed class FakeTemplateService : ITemplateService
    {
        public Task<IReadOnlyList<Template>> ListAsync() => Task.FromResult<IReadOnlyList<Template>>([]);

        public Task<Template> GetAsync(string id) => Task.FromResult<Template>(null);

        public Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
        {
            if (id == AITemplateIds.DocumentContextHeader)
            {
                return Task.FromResult("[Retrieved Document Context]");
            }

            return Task.FromResult($"[Template: {id}]");
        }

        public Task<string> MergeAsync(IEnumerable<string> ids, IDictionary<string, object> arguments = null, string separator = "\n\n")
            => Task.FromResult(string.Join(separator, ids));
    }

    private sealed class FakeAIClientFactory : IAIClientFactory
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

        public FakeAIClientFactory(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _embeddingGenerator = embeddingGenerator;
        }

        public ValueTask<IChatClient> CreateChatClientAsync(string providerName, string connectionName, string deploymentName)
            => new((IChatClient)null);

        public ValueTask<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(string providerName, string connectionName, string deploymentName)
            => new(_embeddingGenerator);

        public ValueTask<IImageGenerator> CreateImageGeneratorAsync(string providerName, string connectionName, string deploymentName = null)
            => new((IImageGenerator)null);

        public ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(string providerName, string connectionName, string deploymentName = null)
            => new((ISpeechToTextClient)null);

        public ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(AIDeployment deployment)
            => new((ISpeechToTextClient)null);

        public ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(string providerName, string connectionName, string deploymentName = null)
            => new((ITextToSpeechClient)null);

        public ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(AIDeployment deployment)
            => new((ITextToSpeechClient)null);
    }

    private sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly float[] _fixedVector;

        public FakeEmbeddingGenerator(float[] fixedVector)
        {
            _fixedVector = fixedVector;
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
                embeddings.Add(new Embedding<float>(_fixedVector));
            }

            return Task.FromResult(embeddings);
        }

        public object GetService(Type serviceType, object serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
