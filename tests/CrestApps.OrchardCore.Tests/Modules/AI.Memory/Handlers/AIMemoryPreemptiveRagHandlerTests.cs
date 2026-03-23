using System.Security.Claims;
using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Memory;
using CrestApps.OrchardCore.AI.Memory.Handlers;
using CrestApps.OrchardCore.AI.Memory.Models;
using CrestApps.OrchardCore.AI.Memory.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Settings;

#pragma warning disable MEAI001 // Text-to-speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.
namespace CrestApps.OrchardCore.Tests.Modules.AI.Memory.Handlers;

public sealed class AIMemoryPreemptiveRagHandlerTests
{
    [Fact]
    public async Task CanHandleAsync_AuthenticatedProfileWithMemoryEnabled_ReturnsTrue()
    {
        var handler = CreateHandler();
        var profile = new AIProfile();
        profile.AlterSettings<AIProfileMemorySettings>(settings => settings.EnableUserMemory = true);

        var canHandle = await handler.CanHandleAsync(new OrchestrationContextBuiltContext(profile, new OrchestrationContext()));

        Assert.True(canHandle);
    }

    [Fact]
    public async Task CanHandleAsync_UnauthenticatedRequest_ReturnsFalse()
    {
        var handler = CreateHandler(userId: null);
        var profile = new AIProfile();
        profile.AlterSettings<AIProfileMemorySettings>(settings => settings.EnableUserMemory = true);

        var canHandle = await handler.CanHandleAsync(new OrchestrationContextBuiltContext(profile, new OrchestrationContext()));

        Assert.False(canHandle);
    }

    [Fact]
    public async Task CanHandleAsync_PreemptiveMemoryRetrievalDisabledInSiteSettings_ReturnsFalse()
    {
        var handler = CreateHandler(siteService: CreateSiteService(enableChatInteractionMemory: true, enablePreemptiveMemoryRetrieval: false));
        var profile = new AIProfile();
        profile.AlterSettings<AIProfileMemorySettings>(settings => settings.EnableUserMemory = true);

        var canHandle = await handler.CanHandleAsync(new OrchestrationContextBuiltContext(profile, new OrchestrationContext()));

        Assert.False(canHandle);
    }

    [Fact]
    public async Task HandleAsync_RelevantMemoriesFound_AppendsMemoryContext()
    {
        var memorySearchService = CreateMemorySearchService(
            [
                new AIMemorySearchResult
                {
                    MemoryId = "memory-1",
                    Name = "preferred_name",
                    Description = "The user's preferred name.",
                    Content = "Mike",
                    UpdatedUtc = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc),
                    Score = 0.98f,
                },
            ]);

        var handler = CreateHandler(memorySearchService: memorySearchService);
        var context = new OrchestrationContext
        {
            DisableTools = false,
            CompletionContext = new AICompletionContext(),
        };

        await handler.HandleAsync(new PreemptiveRagContext(context, new AIProfile(), ["What is my preferred name?"]));

        var systemMessage = context.SystemMessageBuilder.ToString();
        Assert.Contains("[Retrieved User Memory]", systemMessage);
        Assert.Contains("search_user_memories", systemMessage);
        Assert.Contains("Memory: preferred_name", systemMessage);
        Assert.Contains("Description: The user's preferred name.", systemMessage);
        Assert.Contains("Content: Mike", systemMessage);
    }

    [Fact]
    public async Task HandleAsync_DuplicateMatchesAcrossQueries_DeduplicatesByMemoryId()
    {
        var memorySearchService = CreateMemorySearchService(
            [
                new AIMemorySearchResult
                {
                    MemoryId = "memory-1",
                    Name = "preferred_editor",
                    Description = "The user's preferred editor.",
                    Content = "VS Code",
                    Score = 0.80f,
                },
                new AIMemorySearchResult
                {
                    MemoryId = "memory-1",
                    Name = "preferred_editor",
                    Description = "The user's preferred editor.",
                    Content = "VS Code",
                    Score = 0.95f,
                },
            ]);

        var handler = CreateHandler(memorySearchService: memorySearchService);
        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext(),
        };

        await handler.HandleAsync(new PreemptiveRagContext(
            context,
            new AIProfile(),
            ["What editor do I prefer?", "Which IDE do I like?"]));

        var systemMessage = context.SystemMessageBuilder.ToString();
        Assert.Equal(1, CountOccurrences(systemMessage, "Memory: preferred_editor"));
        Assert.Contains("Content: VS Code", systemMessage);
    }

    private static AIMemoryPreemptiveRagHandler CreateHandler(
        string userId = "user-1",
        AIMemorySearchService memorySearchService = null,
        ISiteService siteService = null)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = string.IsNullOrEmpty(userId)
                    ? new ClaimsPrincipal(new ClaimsIdentity())
                    : new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId),
                    ], "TestAuth")),
            },
        };

        return new AIMemoryPreemptiveRagHandler(
            memorySearchService ?? CreateMemorySearchService([]),
            new FakeAITemplateService(),
            siteService ?? CreateSiteService(enableChatInteractionMemory: true),
            httpContextAccessor,
            NullLogger<AIMemoryPreemptiveRagHandler>.Instance);
    }

    private static AIMemorySearchService CreateMemorySearchService(IEnumerable<AIMemorySearchResult> results)
    {
        var indexProfile = new IndexProfile
        {
            Name = "memory-index",
            ProviderName = "test-provider",
        };
        indexProfile.Put(new AIMemoryIndexProfileMetadata
        {
            EmbeddingProviderName = "test-provider",
            EmbeddingConnectionName = "default",
            EmbeddingDeploymentName = "embedding-model",
        });

        var siteService = CreateSiteService(indexProfileName: indexProfile.Name);

        var indexProfileStore = new Mock<IIndexProfileStore>();
        indexProfileStore
            .Setup(store => store.FindByNameAsync(indexProfile.Name))
            .ReturnsAsync(indexProfile);

        var vectorSearchService = new Mock<IMemoryVectorSearchService>();
        vectorSearchService
            .Setup(service => service.SearchAsync(
                indexProfile,
                It.IsAny<float[]>(),
                "user-1",
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var services = new ServiceCollection()
            .AddKeyedSingleton<IMemoryVectorSearchService>("test-provider", vectorSearchService.Object)
            .BuildServiceProvider();

        return new AIMemorySearchService(
            siteService,
            indexProfileStore.Object,
            services,
            new FakeAIClientFactory(new FakeEmbeddingGenerator([0.1f, 0.2f])),
            NullLogger<AIMemorySearchService>.Instance);
    }

    private static ISiteService CreateSiteService(
        bool enableChatInteractionMemory = true,
        string indexProfileName = "memory-index",
        bool enablePreemptiveMemoryRetrieval = true)
    {
        var siteSettings = new Mock<ISite>();
        siteSettings.Setup(site => site.As<AIMemorySettings>())
            .Returns(new AIMemorySettings
            {
                IndexProfileName = indexProfileName,
                TopN = 5,
            });
        siteSettings.Setup(site => site.As<ChatInteractionMemorySettings>())
            .Returns(new ChatInteractionMemorySettings
            {
                EnableUserMemory = enableChatInteractionMemory,
            });
        siteSettings.Setup(site => site.As<GeneralAISettings>())
            .Returns(new GeneralAISettings
            {
                EnablePreemptiveMemoryRetrieval = enablePreemptiveMemoryRetrieval,
            });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync())
            .ReturnsAsync(siteSettings.Object);

        return siteService.Object;
    }

    private static int CountOccurrences(string value, string searchText)
    {
        var count = 0;
        var index = 0;

        while ((index = value.IndexOf(searchText, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += searchText.Length;
        }

        return count;
    }

    private sealed class FakeAITemplateService : IAITemplateService
    {
        public Task<IReadOnlyList<AITemplate>> ListAsync()
            => Task.FromResult<IReadOnlyList<AITemplate>>([]);

        public Task<AITemplate> GetAsync(string id)
            => Task.FromResult<AITemplate>(null);

        public Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
        {
            if (id == MemoryConstants.TemplateIds.MemoryContextHeader &&
                arguments?.TryGetValue("searchToolName", out var searchToolName) == true &&
                arguments.TryGetValue("results", out var resultsObj) == true &&
                resultsObj is IEnumerable<object> results)
            {
                var lines = new List<string>
                {
                    "[Retrieved User Memory]",
                    $"Use `{searchToolName}` for more memory.",
                };

                foreach (dynamic result in results)
                {
                    lines.Add($"Memory: {result.Name}");
                    lines.Add($"Description: {result.Description}");
                    lines.Add($"Content: {result.Content}");
                }

                return Task.FromResult(string.Join(Environment.NewLine, lines));
            }

            return Task.FromResult("[Retrieved User Memory]");
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

#pragma warning disable MEAI001
        public ValueTask<IImageGenerator> CreateImageGeneratorAsync(string providerName, string connectionName, string deploymentName = null)
            => new((IImageGenerator)null);

        public ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(string providerName, string connectionName, string deploymentName = null)
            => new((ISpeechToTextClient)null);

        public ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(AIDeployment deployment)
            => new((ISpeechToTextClient)null);
#pragma warning restore MEAI001

#pragma warning disable MEAI001
        public ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(string providerName, string connectionName, string deploymentName = null)
            => new((ITextToSpeechClient)null);

        public ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(AIDeployment deployment)
            => new((ITextToSpeechClient)null);
#pragma warning restore MEAI001
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
