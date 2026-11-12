using System.Security.Claims;
using System.Threading.Channels;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Chat.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// SEC-A4: a template-prompt profile sent through the chat hub runs with its own system prompt, tools and data
/// sources, so the caller must be allowed to use it as well as the profile of the session, and each message counts
/// against the per-message limit like any other.
/// </summary>
public sealed class AIChatHubTemplatePromptAuthorizationTests
{
    private const string TemplateId = "template-profile";
    private const string ParentId = "parent-profile";

    [Fact]
    public async Task HandleSendMessageAsync_WhenCallerCannotUseTheTemplate_RefusesAndRunsNothing()
    {
        var (hub, caller) = CreateHub(authorizedIds: [ParentId]);

        await hub.SendForTestAsync();

        caller.Verify(client => client.ReceiveError("You are not authorized to interact with the given profile."), Times.Once);
        Assert.False(hub.GeneratedPromptRan);
    }

    [Fact]
    public async Task HandleSendMessageAsync_WhenCallerCannotUseTheParent_RefusesAndRunsNothing()
    {
        var (hub, caller) = CreateHub(authorizedIds: [TemplateId]);

        await hub.SendForTestAsync();

        caller.Verify(client => client.ReceiveError("You are not authorized to interact with the given profile."), Times.Once);
        Assert.False(hub.GeneratedPromptRan);
    }

    [Fact]
    public async Task HandleSendMessageAsync_WhenCallerMayUseBoth_RunsTheTemplate()
    {
        var (hub, caller) = CreateHub(authorizedIds: [TemplateId, ParentId]);

        await hub.SendForTestAsync();

        Assert.True(hub.GeneratedPromptRan);
        caller.Verify(client => client.ReceiveError(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleSendMessageAsync_WhenTheMessageLimitIsReached_RefusesAndRunsNothing()
    {
        var rateLimiter = new Mock<IChatRateLimiter>();
        rateLimiter
            .Setup(limiter => limiter.EvaluateAsync(It.Is<PromptSecurityContext>(context => context.ProfileId == ParentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitResult.Throttled(30, 21, 20));

        var (hub, caller) = CreateHub(authorizedIds: [TemplateId, ParentId], rateLimiter.Object);

        await hub.SendForTestAsync();

        Assert.False(hub.GeneratedPromptRan);
        caller.Verify(client => client.ReceiveError(It.Is<string>(message => message.Contains("30", StringComparison.Ordinal))), Times.Once);
    }

    [Fact]
    public async Task GetAccessibleProfilesAsync_ReturnsOnlyTheProfilesTheCallerMayUse()
    {
        var allowed = new AIProfile { ItemId = "allowed", Type = AIProfileType.TemplatePrompt };
        var denied = new AIProfile { ItemId = "denied", Type = AIProfileType.TemplatePrompt };
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object resource, IEnumerable<IAuthorizationRequirement> _) =>
                ReferenceEquals(resource, allowed) ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var evaluator = new AIChatProfileAccessEvaluator(authorizationService.Object);

        var profiles = await evaluator.GetAccessibleProfilesAsync(new ClaimsPrincipal(), [allowed, denied]);

        Assert.Equal([allowed], profiles);
    }

    private static (TestAIChatHub Hub, Mock<IAIChatHubClient> Caller) CreateHub(string[] authorizedIds, IChatRateLimiter rateLimiter = null)
    {
        var profileManager = new Mock<IAIProfileManager>();
        profileManager
            .Setup(manager => manager.FindByIdAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProfile { ItemId = TemplateId, Type = AIProfileType.TemplatePrompt });
        profileManager
            .Setup(manager => manager.FindByIdAsync(ParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProfile { ItemId = ParentId, Type = AIProfileType.Chat });

        var serviceCollection = new ServiceCollection()
            .AddSingleton(profileManager.Object);

        if (rateLimiter is not null)
        {
            serviceCollection.AddSingleton(rateLimiter);
        }

        var services = serviceCollection.BuildServiceProvider();

        var caller = new Mock<IAIChatHubClient>();
        var clients = new Mock<IHubCallerClients<IAIChatHubClient>>();
        clients.SetupGet(c => c.Caller).Returns(caller.Object);

        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns("connection");
        context.SetupGet(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));
        context.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);

        var hub = new TestAIChatHub(services, authorizedIds)
        {
            Clients = clients.Object,
            Context = context.Object,
        };

        return (hub, caller);
    }

    private sealed class TestAIChatHub : AIChatHub
    {
        private readonly IServiceProvider _services;
        private readonly HashSet<string> _authorizedIds;

        public TestAIChatHub(IServiceProvider services, string[] authorizedIds)
            : base(services, TimeProvider.System, NullLogger<AIChatHub>.Instance, new NullStringLocalizer())
        {
            _services = services;
            _authorizedIds = [.. authorizedIds];
        }

        public bool GeneratedPromptRan { get; private set; }

        public Task SendForTestAsync()
            => HandleSendMessageAsync(
                Channel.CreateUnbounded<CompletionPartialMessage>().Writer,
                _services,
                TemplateId,
                prompt: null,
                sessionId: "session",
                sessionProfileId: ParentId,
                CancellationToken.None);

        protected override Task<bool> AuthorizeProfileAsync(IServiceProvider services, AIProfile profile)
            => Task.FromResult(_authorizedIds.Contains(profile.ItemId));

        protected override Task ProcessGeneratedPromptAsync(
            ChannelWriter<CompletionPartialMessage> writer,
            IServiceProvider services,
            AIProfile profile,
            string sessionId,
            AIProfile parentProfile,
            CancellationToken cancellationToken)
        {
            GeneratedPromptRan = true;

            return Task.CompletedTask;
        }
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
