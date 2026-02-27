using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class PreemptiveRagOrchestrationHandlerTests
{
    /// <summary>
    /// When no IPreemptiveRagHandler implementations are registered,
    /// the handler should return immediately without modifying the system message.
    /// </summary>
    [Fact]
    public async Task BuiltAsync_NoHandlers_DoesNotModifySystemMessage()
    {
        var handler = CreateHandler([], enablePreemptiveRag: true);
        var context = CreateOrchestrationContext(userMessage: "test query");

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new AIProfile(), context));

        Assert.Equal(string.Empty, context.SystemMessageBuilder.ToString());
    }

    /// <summary>
    /// When the user message is empty, the handler returns early.
    /// </summary>
    [Fact]
    public async Task BuiltAsync_EmptyUserMessage_DoesNotModifySystemMessage()
    {
        var fakeHandler = CreateFakeRagHandler(canHandle: true);
        var handler = CreateHandler([fakeHandler.Object], enablePreemptiveRag: true);
        var context = CreateOrchestrationContext(userMessage: "");

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new AIProfile(), context));

        Assert.Equal(string.Empty, context.SystemMessageBuilder.ToString());
        fakeHandler.Verify(h => h.CanHandleAsync(It.IsAny<OrchestrationContextBuiltContext>()), Times.Never);
    }

    /// <summary>
    /// When no handlers pass CanHandle, no queries are extracted and system message is untouched.
    /// </summary>
    [Fact]
    public async Task BuiltAsync_NoUsableHandlers_DoesNotModifySystemMessage()
    {
        var fakeHandler = CreateFakeRagHandler(canHandle: false);
        var handler = CreateHandler([fakeHandler.Object], enablePreemptiveRag: true);
        var context = CreateOrchestrationContext(userMessage: "test query");

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new AIProfile(), context));

        Assert.Equal(string.Empty, context.SystemMessageBuilder.ToString());
    }

    /// <summary>
    /// When preemptive RAG is disabled and tools are available (DisableTools=false),
    /// the handler injects tool search instructions using the relaxed template
    /// when IsInScope is not set (default).
    /// </summary>
    [Fact]
    public async Task BuiltAsync_PreemptiveRagDisabled_ToolsAvailable_InjectsRelaxedSearch()
    {
        var fakeHandler = CreateFakeRagHandler(canHandle: true);
        var handler = CreateHandler([fakeHandler.Object], enablePreemptiveRag: false);
        var context = CreateOrchestrationContext(userMessage: "test query", disableTools: false);

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new AIProfile(), context));

        var message = context.SystemMessageBuilder.ToString();
        Assert.Contains($"[Template: {AITemplateIds.RagToolSearchRelaxed}]", message);
    }

    /// <summary>
    /// When preemptive RAG is disabled, tools are available, and IsInScope=true,
    /// the handler injects the strict tool search template.
    /// </summary>
    [Fact]
    public async Task BuiltAsync_PreemptiveRagDisabled_ToolsAvailable_IsInScope_InjectsStrictSearch()
    {
        var fakeHandler = CreateFakeRagHandler(canHandle: true);
        var handler = CreateHandler([fakeHandler.Object], enablePreemptiveRag: false);
        var context = CreateOrchestrationContext(userMessage: "test query", disableTools: false);

        var profile = new AIProfile();
        profile.Put(new AIDataSourceRagMetadata { IsInScope = true });

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(profile, context));

        var message = context.SystemMessageBuilder.ToString();
        Assert.Contains($"[Template: {AITemplateIds.RagToolSearchStrict}]", message);
    }

    /// <summary>
    /// When IsInScope is false and references exist, the handler injects
    /// the RagResponseGuidelines template.
    /// </summary>
    [Fact]
    public async Task BuiltAsync_IsInScopeFalse_HasRefs_InjectsResponseGuidelines()
    {
        var fakeHandler = CreateFakeRagHandler(canHandle: true, addDataSourceRefs: true);
        var handler = CreateHandler([fakeHandler.Object], enablePreemptiveRag: true);
        var context = CreateOrchestrationContext(userMessage: "test query");

        var profile = new AIProfile();
        // No AIDataSourceRagMetadata → IsInScope is null (not true).

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(profile, context));

        var message = context.SystemMessageBuilder.ToString();
        Assert.Contains($"[Template: {AITemplateIds.RagResponseGuidelines}]", message);
    }

    /// <summary>
    /// When IsInScope is true and no refs are produced with tools disabled,
    /// the handler injects RagScopeNoRefsToolsDisabled.
    /// </summary>
    [Fact]
    public async Task BuiltAsync_IsInScopeTrue_NoRefs_ToolsDisabled_InjectsNoRefsToolsDisabled()
    {
        var fakeHandler = CreateFakeRagHandler(canHandle: true, addDataSourceRefs: false);
        var handler = CreateHandler([fakeHandler.Object], enablePreemptiveRag: true);
        var context = CreateOrchestrationContext(userMessage: "test query", disableTools: true);

        var profile = new AIProfile();
        profile.Put(new AIDataSourceRagMetadata { IsInScope = true });

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(profile, context));

        var message = context.SystemMessageBuilder.ToString();
        Assert.Contains($"[Template: {AITemplateIds.RagScopeNoRefsToolsDisabled}]", message);
    }

    /// <summary>
    /// When IsInScope is true and no refs are produced with tools enabled,
    /// the handler injects RagScopeNoRefsToolsEnabled.
    /// </summary>
    [Fact]
    public async Task BuiltAsync_IsInScopeTrue_NoRefs_ToolsEnabled_InjectsNoRefsToolsEnabled()
    {
        var fakeHandler = CreateFakeRagHandler(canHandle: true, addDataSourceRefs: false);
        var handler = CreateHandler([fakeHandler.Object], enablePreemptiveRag: true);
        var context = CreateOrchestrationContext(userMessage: "test query", disableTools: false);

        var profile = new AIProfile();
        profile.Put(new AIDataSourceRagMetadata { IsInScope = true });

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(profile, context));

        var message = context.SystemMessageBuilder.ToString();
        Assert.Contains($"[Template: {AITemplateIds.RagScopeNoRefsToolsEnabled}]", message);
    }

    /// <summary>
    /// When IsInScope is true and refs are present,
    /// the handler injects RagScopeWithRefs.
    /// </summary>
    [Fact]
    public async Task BuiltAsync_IsInScopeTrue_HasRefs_InjectsRagScopeWithRefs()
    {
        var fakeHandler = CreateFakeRagHandler(canHandle: true, addDataSourceRefs: true);
        var handler = CreateHandler([fakeHandler.Object], enablePreemptiveRag: true);
        var context = CreateOrchestrationContext(userMessage: "test query", disableTools: false);

        var profile = new AIProfile();
        profile.Put(new AIDataSourceRagMetadata { IsInScope = true });

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(profile, context));

        var message = context.SystemMessageBuilder.ToString();
        Assert.Contains($"[Template: {AITemplateIds.RagScopeWithRefs}]", message);
    }

    /// <summary>
    /// When IsInScope is false and no references exist,
    /// no template is injected (silent return).
    /// </summary>
    [Fact]
    public async Task BuiltAsync_IsInScopeFalse_NoRefs_DoesNotInjectTemplate()
    {
        var fakeHandler = CreateFakeRagHandler(canHandle: true, addDataSourceRefs: false);
        var handler = CreateHandler([fakeHandler.Object], enablePreemptiveRag: true);
        var context = CreateOrchestrationContext(userMessage: "test query");

        var profile = new AIProfile();
        // No AIDataSourceRagMetadata → IsInScope is null (not true).

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(profile, context));

        Assert.Equal(string.Empty, context.SystemMessageBuilder.ToString());
    }

    /// <summary>
    /// Verifies ChatInteraction resource works with IsInScope via TryGet.
    /// </summary>
    [Fact]
    public async Task BuiltAsync_ChatInteractionResource_IsInScopeTrue_HasRefs_InjectsRagScopeWithRefs()
    {
        var fakeHandler = CreateFakeRagHandler(canHandle: true, addDataSourceRefs: true);
        var handler = CreateHandler([fakeHandler.Object], enablePreemptiveRag: true);
        var context = CreateOrchestrationContext(userMessage: "test query");

        var interaction = new ChatInteraction();
        interaction.Put(new AIDataSourceRagMetadata { IsInScope = true });

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(interaction, context));

        var message = context.SystemMessageBuilder.ToString();
        Assert.Contains($"[Template: {AITemplateIds.RagScopeWithRefs}]", message);
    }

    private static PreemptiveRagOrchestrationHandler CreateHandler(
        IPreemptiveRagHandler[] handlers,
        bool enablePreemptiveRag)
    {
        var templateService = new FakeAITemplateService();
        var queryProvider = new PreemptiveSearchQueryProvider(
            new NullAIClientFactory(),
            templateService,
            Options.Create(new AIProviderOptions()),
            NullLogger<PreemptiveSearchQueryProvider>.Instance);

        var siteMock = new Mock<ISiteService>();
        var siteModelMock = new Mock<ISite>();
        siteModelMock.Setup(s => s.As<DefaultOrchestratorSettings>())
            .Returns(new DefaultOrchestratorSettings { EnablePreemptiveRag = enablePreemptiveRag });
        siteMock.Setup(s => s.GetSiteSettingsAsync())
            .ReturnsAsync(siteModelMock.Object);

        return new PreemptiveRagOrchestrationHandler(
            handlers,
            queryProvider,
            templateService,
            siteMock.Object,
            NullLogger<PreemptiveRagOrchestrationHandler>.Instance);
    }

    private static Mock<IPreemptiveRagHandler> CreateFakeRagHandler(bool canHandle, bool addDataSourceRefs = false)
    {
        var mock = new Mock<IPreemptiveRagHandler>();
        mock.Setup(h => h.CanHandleAsync(It.IsAny<OrchestrationContextBuiltContext>()))
            .ReturnsAsync(canHandle);

        if (addDataSourceRefs)
        {
            mock.Setup(h => h.HandleAsync(It.IsAny<PreemptiveRagContext>()))
                .Callback<PreemptiveRagContext>(ctx =>
                {
                    ctx.OrchestrationContext.Properties["DataSourceReferences"] = "refs";
                })
                .Returns(Task.CompletedTask);
        }

        return mock;
    }

    private static OrchestrationContext CreateOrchestrationContext(
        string userMessage = "",
        bool disableTools = false)
    {
        return new OrchestrationContext
        {
            UserMessage = userMessage,
            DisableTools = disableTools,
            CompletionContext = new AICompletionContext(),
        };
    }

    private sealed class FakeAITemplateService : IAITemplateService
    {
        public Task<IReadOnlyList<AITemplate>> ListAsync()
            => Task.FromResult<IReadOnlyList<AITemplate>>([]);

        public Task<AITemplate> GetAsync(string id)
            => Task.FromResult<AITemplate>(null);

        public Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
            => Task.FromResult($"[Template: {id}]");

        public Task<string> MergeAsync(IEnumerable<string> ids, IDictionary<string, object> arguments = null, string separator = "\n\n")
            => Task.FromResult(string.Join(separator, ids.Select(id => $"[Template: {id}]")));
    }
}
