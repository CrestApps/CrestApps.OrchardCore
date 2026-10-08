using System.Threading.Channels;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Models;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Exceptions;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// <see cref="AIChatHub"/> replaces the framework's <c>ProcessGeneratedPromptAsync</c> outright to
/// render Liquid templates, so it has to repeat the base class's session-start error handling. These
/// tests pin that routing, which the framework's own tests cannot reach.
/// </summary>
public sealed class AIChatHubGeneratedPromptSessionStartTests
{
    [Fact]
    public async Task ProcessGeneratedPromptAsync_WhenSessionStartThrottled_SignalsSessionStartRejected()
    {
        var (hub, caller) = CreateHub(new ChatSessionStartRateLimitedException("Too many new chats."));

        await hub.ProcessGeneratedPromptForTestAsync();

        caller.Verify(client => client.ReceiveSessionStartRejected("Too many new chats."), Times.Once);
        caller.Verify(client => client.ReceiveError(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessGeneratedPromptAsync_WhenSessionStartFails_SendsGenericError()
    {
        var (hub, caller) = CreateHub(new InvalidOperationException("Profile not found."));

        await hub.ProcessGeneratedPromptForTestAsync();

        caller.Verify(client => client.ReceiveError("Profile not found."), Times.Once);
        caller.Verify(client => client.ReceiveSessionStartRejected(It.IsAny<string>()), Times.Never);
    }

    private static (TestAIChatHub Hub, Mock<IAIChatHubClient> Caller) CreateHub(Exception sessionStartFailure)
    {
        var services = new ServiceCollection()
            .AddSingleton(new Mock<IAIChatSessionManager>().Object)
            .AddSingleton(new Mock<IAIChatSessionPromptStore>().Object)
            .AddSingleton(new Mock<ILiquidTemplateManager>().Object)
            .AddSingleton(new Mock<IAICompletionContextBuilder>().Object)
            .AddSingleton(new Mock<IAICompletionService>().Object)
            .BuildServiceProvider();

        var caller = new Mock<IAIChatHubClient>();
        var clients = new Mock<IHubCallerClients<IAIChatHubClient>>();
        clients.SetupGet(c => c.Caller).Returns(caller.Object);

        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns("connection");
        context.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);

        var hub = new TestAIChatHub(services, sessionStartFailure)
        {
            Clients = clients.Object,
            Context = context.Object,
        };

        return (hub, caller);
    }

    private sealed class TestAIChatHub : AIChatHub
    {
        private readonly IServiceProvider _services;
        private readonly Exception _sessionStartFailure;

        public TestAIChatHub(IServiceProvider services, Exception sessionStartFailure)
            : base(services, TimeProvider.System, NullLogger<AIChatHub>.Instance, new NullStringLocalizer())
        {
            _services = services;
            _sessionStartFailure = sessionStartFailure;
        }

        public Task ProcessGeneratedPromptForTestAsync()
            => ProcessGeneratedPromptAsync(
                Channel.CreateUnbounded<CompletionPartialMessage>().Writer,
                _services,
                new AIProfile { ItemId = "template-profile" },
                sessionId: null,
                parentProfile: new AIProfile { ItemId = "parent-profile" },
                CancellationToken.None);

        protected override Task<(AIChatSession ChatSession, bool IsNewSession)> GetOrCreateSessionAsync(
            IServiceProvider services,
            string sessionId,
            AIProfile profile,
            string userPrompt)
            => Task.FromException<(AIChatSession, bool)>(_sessionStartFailure);
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
