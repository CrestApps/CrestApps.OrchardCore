using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using AGUI.Abstractions;
using CrestApps.Core.AI;
using CrestApps.Core.AI.AGUI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.AGUI;
using CrestApps.Core.AI.Chat.Models;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using Fluid.Values;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// <see cref="AIChatHub"/> replaces the framework's <c>ProcessGeneratedPromptAsync</c> to render Liquid templates, so it
/// has to repeat the base method's AG-UI branch. An AG-UI turn's writer sends every write after the first under one
/// message id as "message replaced", so a template prompt streamed through it showed only its last chunk.
/// </summary>
public sealed class AIChatHubGeneratedPromptAGUITests
{
    [Fact]
    public async Task ProcessGeneratedPromptAsync_OnAnAGUITurn_StreamsEveryChunkAsText()
    {
        var events = new List<BaseEvent>();
        var turn = new AGUIChatTurn(null, new AGUIRunEventWriter(null, "run-1", (evt, _) =>
        {
            events.Add(evt);

            return ValueTask.CompletedTask;
        }));

        var promptStore = new Mock<IAIChatSessionPromptStore>();
        AIChatSessionPrompt saved = null;
        promptStore.Setup(store => store.CreateAsync(It.IsAny<AIChatSessionPrompt>(), It.IsAny<CancellationToken>()))
            .Callback<AIChatSessionPrompt, CancellationToken>((prompt, _) => saved = prompt)
            .Returns(ValueTask.CompletedTask);

        var hub = CreateHub(promptStore.Object, ["Hello", " there", ", friend."]);

        await hub.ProcessGeneratedPromptForTestAsync(turn);

        var text = string.Concat(events.OfType<TextMessageContentEvent>().Select(evt => evt.Delta));

        Assert.Equal("Hello there, friend.", text);
        Assert.DoesNotContain(events.OfType<CustomEvent>(), evt => evt.Name == AGUIChatTurn.MessageReplacedEventName);
        Assert.Equal("session-1", turn.Events.ThreadId);
        Assert.NotNull(saved);
        Assert.Equal("Hello there, friend.", saved.Content);
    }

    private static TestAIChatHub CreateHub(IAIChatSessionPromptStore promptStore, string[] chunks)
    {
        var liquid = new Mock<ILiquidTemplateManager>();
        liquid.Setup(manager => manager.RenderStringAsync(It.IsAny<string>(), It.IsAny<TextEncoder>(), It.IsAny<object>(), It.IsAny<IEnumerable<KeyValuePair<string, FluidValue>>>()))
            .ReturnsAsync("Write a greeting.");

        var contextBuilder = new Mock<IAICompletionContextBuilder>();
        contextBuilder.Setup(builder => builder.BuildAsync(It.IsAny<object>(), It.IsAny<Action<AICompletionContext>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AICompletionContext());

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager.Setup(manager => manager.ResolveSlotAsync(
                AIDeploymentSlotNames.Chat,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIDeployment { Name = "Chat" });

        var completionService = new Mock<IAICompletionService>();
        completionService.Setup(service => service.CompleteStreamingAsync(It.IsAny<AIDeployment>(), It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<AICompletionContext>(), It.IsAny<CancellationToken>()))
            .Returns((AIDeployment _, IEnumerable<ChatMessage> _, AICompletionContext _, CancellationToken cancellationToken) => StreamAsync(chunks, cancellationToken));

        var services = new ServiceCollection()
            .AddSingleton(new Mock<IAIChatSessionManager>().Object)
            .AddSingleton(promptStore)
            .AddSingleton(liquid.Object)
            .AddSingleton(contextBuilder.Object)
            .AddSingleton(completionService.Object)
            .AddSingleton(deploymentManager.Object)
            .BuildServiceProvider();

        var clients = new Mock<IHubCallerClients<IAIChatHubClient>>();
        clients.SetupGet(c => c.Caller).Returns(new Mock<IAIChatHubClient>().Object);

        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns("connection");
        context.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);

        return new TestAIChatHub(services)
        {
            Clients = clients.Object,
            Context = context.Object,
        };
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(string[] chunks, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
        }
    }

    private sealed class TestAIChatHub : AIChatHub
    {
        private readonly IServiceProvider _services;

        public TestAIChatHub(IServiceProvider services)
            : base(services, TimeProvider.System, NullLogger<AIChatHub>.Instance, new NullStringLocalizer())
        {
            _services = services;
        }

        public Task ProcessGeneratedPromptForTestAsync(AGUIChatTurn turn)
            => ProcessGeneratedPromptAsync(
                turn,
                _services,
                new AIProfile { ItemId = "template-profile", Name = "Greeting", PromptTemplate = "Write a greeting." },
                sessionId: "session-1",
                parentProfile: new AIProfile { ItemId = "parent-profile" },
                CancellationToken.None);

        protected override Task<(AIChatSession ChatSession, bool IsNewSession)> GetOrCreateSessionAsync(
            IServiceProvider services,
            string sessionId,
            AIProfile profile,
            string userPrompt)
            => Task.FromResult((new AIChatSession { SessionId = "session-1", ProfileId = profile.ItemId }, false));

        protected override Task SaveChatSessionAsync(IServiceProvider services, IAIChatSessionManager sessionManager, AIChatSession chatSession)
            => Task.CompletedTask;
    }

    private sealed class NullStringLocalizer : IStringLocalizer<AIChatHub>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
