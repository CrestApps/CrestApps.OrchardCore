using System.Security.Claims;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// Bug: reviewing an automated SMS or voice conversation ended with a "Session not found." bubble and nothing in the
/// logs to say why: the hub's lookup is user-scoped and a system-owned session has no user. The page no longer asks
/// the hub for such a session, and the hub's <see cref="AIChatHub.LoadSession(string)"/> now logs the cause while
/// still answering the caller with the generic error, so it never reveals whether someone else's session exists.
/// </summary>
public sealed class AIChatHubLoadSessionTests
{
    [Fact]
    public async Task LoadSession_ASystemOwnedSession_SendsNotFoundAndLogsWhyItCannotLoad()
    {
        // Arrange
        var harness = new Harness(
            ownSession: null,
            storedSession: new AIChatSession { SessionId = "session-1", ProfileId = "profile-1", UserId = null, ClientId = null });

        // Act
        await harness.Hub.LoadSession("session-1");

        // Assert
        harness.Caller.Verify(client => client.ReceiveError("Session not found."), Times.Once);
        Assert.DoesNotContain(harness.Caller.Invocations, invocation => invocation.Method.Name == nameof(IAIChatHubClient.LoadSession));
        harness.Groups.Verify(
            groups => groups.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var warning = Assert.Single(harness.Logger.At(LogLevel.Warning));
        Assert.Contains("session-1", warning);
        Assert.Contains("is system-owned", warning);
    }

    // The other misses keep the same generic error for the caller but name their own cause in the log.
    [Fact]
    public async Task LoadSession_AnotherUsersSession_SendsNotFoundAndLogsItBelongsToAnotherOwner()
    {
        // Arrange
        var harness = new Harness(
            ownSession: null,
            storedSession: new AIChatSession { SessionId = "session-1", ProfileId = "profile-1", UserId = "user-2" });

        // Act
        await harness.Hub.LoadSession("session-1");

        // Assert
        harness.Caller.Verify(client => client.ReceiveError("Session not found."), Times.Once);
        Assert.Contains("belongs to another owner", Assert.Single(harness.Logger.At(LogLevel.Warning)));
    }

    [Fact]
    public async Task LoadSession_TheCallersOwnSession_JoinsItsGroupAndSendsThePayload()
    {
        // Arrange
        var session = new AIChatSession { SessionId = "session-1", ProfileId = "profile-1", UserId = "user-1" };
        var harness = new Harness(ownSession: session, storedSession: session);

        // Act
        await harness.Hub.LoadSession("session-1");

        // Assert
        harness.Groups.Verify(
            groups => groups.AddToGroupAsync("connection-1", It.Is<string>(name => name.Contains("session-1")), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Single(harness.Caller.Invocations, invocation => invocation.Method.Name == nameof(IAIChatHubClient.LoadSession));
        harness.Caller.Verify(client => client.ReceiveError(It.IsAny<string>()), Times.Never);
        Assert.Empty(harness.Logger.At(LogLevel.Warning));
    }

    private sealed class Harness
    {
        public Harness(AIChatSession ownSession, AIChatSession storedSession)
        {
            var sessionManager = new Mock<IAIChatSessionManager>();
            sessionManager
                .Setup(manager => manager.FindAsync("session-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownSession);
            sessionManager
                .Setup(manager => manager.FindByIdAsync("session-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedSession);

            var profileManager = new Mock<IAIProfileManager>();
            profileManager
                .Setup(manager => manager.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIProfile { ItemId = "profile-1", Name = "support" });

            var promptStore = new Mock<IAIChatSessionPromptStore>();
            promptStore
                .Setup(store => store.GetPromptsAsync("session-1"))
                .ReturnsAsync([]);

            var services = new ServiceCollection()
                .AddSingleton(sessionManager.Object)
                .AddSingleton(profileManager.Object)
                .AddSingleton(promptStore.Object)
                .BuildServiceProvider();

            var clients = new Mock<IHubCallerClients<IAIChatHubClient>>();
            clients.SetupGet(c => c.Caller).Returns(Caller.Object);

            var context = new Mock<HubCallerContext>();
            context.SetupGet(c => c.ConnectionId).Returns("connection-1");
            context.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);
            context.SetupGet(c => c.Features).Returns(new FeatureCollection());
            context.SetupGet(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")));

            Hub = new TestAIChatHub(services, Logger)
            {
                Clients = clients.Object,
                Context = context.Object,
                Groups = Groups.Object,
            };
        }

        public TestAIChatHub Hub { get; }

        public Mock<IAIChatHubClient> Caller { get; } = new();

        public Mock<IGroupManager> Groups { get; } = new();

        public RecordingLogger<AIChatHub> Logger { get; } = new();
    }

    // The production hub runs each invocation in an Orchard shell child scope and authorizes the profile through the
    // access evaluator; the test runs the invocation directly on the test services and allows the profile, so what is
    // exercised is the lookup, the logging and what the caller is sent.
    private sealed class TestAIChatHub : AIChatHub
    {
        private readonly IServiceProvider _services;

        public TestAIChatHub(IServiceProvider services, ILogger<AIChatHub> logger)
            : base(services, TimeProvider.System, logger, new PassThroughStringLocalizer<AIChatHub>())
        {
            _services = services;
        }

        protected override Task ExecuteInScopeAsync(Func<IServiceProvider, Task> action)
            => action(_services);

        protected override Task<bool> AuthorizeProfileAsync(IServiceProvider services, AIProfile profile)
            => Task.FromResult(true);
    }
}
