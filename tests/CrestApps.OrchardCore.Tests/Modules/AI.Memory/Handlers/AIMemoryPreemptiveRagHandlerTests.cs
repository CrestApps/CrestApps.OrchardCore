using System.Security.Claims;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

#pragma warning disable MEAI001

namespace CrestApps.OrchardCore.Tests.Modules.AI.Memory.Handlers;

public sealed class AIMemoryPreemptiveRagHandlerTests
{
    [Fact]
    public async Task CanHandleAsync_AuthenticatedProfileWithMemoryEnabled_ReturnsTrue()
    {
        var handler = CreateHandler();
        var profile = new AIProfile();
        profile.AlterMemoryMetadata(settings => settings.EnableUserMemory = true);

        var canHandle = await handler.CanHandleAsync(new OrchestrationContextBuiltContext(profile, new OrchestrationContext()));

        Assert.True(canHandle);
    }

    [Fact]
    public async Task CanHandleAsync_PreemptiveMemoryRetrievalDisabled_ReturnsFalse()
    {
        var handler = CreateHandler(enablePreemptiveMemoryRetrieval: false);
        var profile = new AIProfile();
        profile.AlterMemoryMetadata(settings => settings.EnableUserMemory = true);

        var canHandle = await handler.CanHandleAsync(new OrchestrationContextBuiltContext(profile, new OrchestrationContext()));

        Assert.False(canHandle);
    }

    [Fact]
    public async Task HandleAsync_RelevantMemoriesFound_AppendsMemoryContext()
    {
        var handler = CreateHandler(
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

    private static AIMemoryPreemptiveRagHandler CreateHandler(
        IEnumerable<AIMemorySearchResult> results = null,
        string userId = "user-1",
        bool enableChatInteractionMemory = true,
        bool enablePreemptiveMemoryRetrieval = true)
    {
        var memorySearchService = new Mock<IAIMemorySearchService>();
        memorySearchService
            .Setup(service => service.SearchAsync(
                "user-1",
                It.IsAny<IEnumerable<string>>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results ?? []);

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
            memorySearchService.Object,
            new FakeAITemplateService(),
            Options.Create(new GeneralAIOptions
            {
                EnablePreemptiveMemoryRetrieval = enablePreemptiveMemoryRetrieval,
            }),
            Options.Create(new ChatInteractionMemoryOptions
            {
                EnableUserMemory = enableChatInteractionMemory,
            }),
            httpContextAccessor,
            NullLogger<AIMemoryPreemptiveRagHandler>.Instance);
    }

    private sealed class FakeAITemplateService : ITemplateService
    {
        public Task<IReadOnlyList<Template>> ListAsync()
            => Task.FromResult<IReadOnlyList<Template>>([]);

        public Task<Template> GetAsync(string id)
            => Task.FromResult<Template>(null);

        public Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
        {
            if (id == MemoryConstants.TemplateIds.MemoryContextHeader)
            {
                var results = arguments["results"] as IEnumerable<object>;
                var lines = new List<string>
                {
                    "[Retrieved User Memory]",
                };

                if (arguments.TryGetValue("searchToolName", out var toolName))
                {
                    lines.Add(toolName?.ToString());
                }

                foreach (var result in results)
                {
                    var type = result.GetType();
                    lines.Add($"Memory: {type.GetProperty("Name")?.GetValue(result)}");
                    lines.Add($"Description: {type.GetProperty("Description")?.GetValue(result)}");
                    lines.Add($"Content: {type.GetProperty("Content")?.GetValue(result)}");
                }

                return Task.FromResult(string.Join(Environment.NewLine, lines));
            }

            return Task.FromResult(string.Empty);
        }

        public Task<string> MergeAsync(IEnumerable<string> ids, IDictionary<string, object> arguments = null, string separator = null)
            => Task.FromResult(string.Empty);
    }
}
