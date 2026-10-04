using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Services;
using CrestApps.OrchardCore.Tests.Omnichannel.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Locking;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;

public sealed class SmsEntryPointAIConversationStarterTests
{
    private const string ProfileId = "front-desk";
    private const string AddressId = "line-1";
    private const string Customer = "+17025550142";

    private static readonly DateTime _now = new(2026, 10, 4, 22, 0, 0, DateTimeKind.Utc);

    // The customer's first text to a number routed to an AI agent starts an automated SMS conversation the AI handler
    // answers: the AI profile and a new chat session on the activity, waiting on the AI's reply to the customer.
    [Fact]
    public async Task FirstText_ToANumberRoutedToAnAIAgent_StartsAnAutomatedConversation()
    {
        var harness = new Harness();

        var started = await harness.Starter.TryStartAsync(Harness.Text(), Harness.Address, TestContext.Current.CancellationToken);

        Assert.True(started);

        var activity = Assert.Single(harness.CreatedActivities);
        Assert.Equal(ActivityInteractionType.Automated, activity.InteractionType);
        Assert.Equal(OmnichannelConstants.Channels.Sms, activity.Channel);
        Assert.Equal(ActivityKind.Sms, activity.Kind);
        Assert.Equal(ActivitySources.Inbound, activity.Source);
        Assert.Equal(AddressId, activity.ChannelEndpointId);
        Assert.Equal(Customer, activity.PreferredDestination);
        Assert.Equal(ProfileId, activity.AIProfileId);
        Assert.Equal(ActivityStatus.AwaitingCustomerAnswer, activity.Status);

        var session = Assert.Single(harness.SavedSessions);
        Assert.Equal(session.SessionId, activity.AISessionId);
        Assert.Equal(ProfileId, session.ProfileId);

        // Committed before the lock is released, so the other handler of this text, and the next text, find it.
        harness.Session.Verify(session => session.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // The inbound SMS subject set up for the number supplies the subject and campaign, as the number's subject does for
    // the calls an AI voice agent answers.
    [Fact]
    public async Task FirstText_UsesTheInboundSmsSubjectOfTheNumber()
    {
        var harness = new Harness
        {
            Flows =
            [
                new SubjectFlowSettings { SubjectContentType = "PhoneInquiry", Channel = OmnichannelConstants.Channels.Phone, ChannelEndpointId = AddressId },
                new SubjectFlowSettings { SubjectContentType = "TextInquiry", Channel = OmnichannelConstants.Channels.Sms, ChannelEndpointId = AddressId, CampaignId = "spring" },
            ],
        };

        await harness.Starter.TryStartAsync(Harness.Text(), Harness.Address, TestContext.Current.CancellationToken);

        var activity = Assert.Single(harness.CreatedActivities);
        Assert.Equal("TextInquiry", activity.SubjectContentType);
        Assert.Equal("spring", activity.CampaignId);
    }

    [Fact]
    public async Task Text_ToANumberNotRoutedToAnAIAgent_StartsNothing()
    {
        var harness = new Harness { Routing = new MessagingInboundRouting { TargetId = "queue-1" } };

        var started = await harness.Starter.TryStartAsync(Harness.Text(), Harness.Address, TestContext.Current.CancellationToken);

        Assert.False(started);
        Assert.Empty(harness.CreatedActivities);
    }

    // A later text while the AI is still talking with the customer belongs to that conversation: no second one starts.
    [Fact]
    public async Task Text_WhileTheAIConversationIsLive_JoinsIt()
    {
        var harness = new Harness
        {
            Latest = new OmnichannelActivity { ItemId = "live", Status = ActivityStatus.AwaitingCustomerAnswer },
        };

        var started = await harness.Starter.TryStartAsync(Harness.Text(), Harness.Address, TestContext.Current.CancellationToken);

        Assert.True(started);
        Assert.Empty(harness.CreatedActivities);
    }

    // A "thanks" right after the AI said goodbye must not open another conversation; it goes to people.
    [Fact]
    public async Task Text_SoonAfterTheLastAIConversationEnded_IsLeftToPeople()
    {
        var harness = new Harness
        {
            Latest = new OmnichannelActivity { ItemId = "ended", Status = ActivityStatus.Completed, CompletedUtc = _now.AddMinutes(-5) },
        };

        var started = await harness.Starter.TryStartAsync(Harness.Text("thanks!"), Harness.Address, TestContext.Current.CancellationToken);

        Assert.False(started);
        Assert.Empty(harness.CreatedActivities);
    }

    [Fact]
    public async Task Text_LongAfterTheLastAIConversationEnded_StartsANewOne()
    {
        var harness = new Harness
        {
            Latest = new OmnichannelActivity { ItemId = "ended", Status = ActivityStatus.Completed, CompletedUtc = _now.AddDays(-2) },
        };

        var started = await harness.Starter.TryStartAsync(Harness.Text(), Harness.Address, TestContext.Current.CancellationToken);

        Assert.True(started);
        Assert.Single(harness.CreatedActivities);
    }

    // A person already talking with the customer keeps the thread; an AI answering it would talk over them.
    [Theory]
    [InlineData(ConversationStatus.Open)]
    [InlineData(ConversationStatus.Snoozed)]
    [InlineData(ConversationStatus.Spam)]
    public async Task Text_WhileAHumanConversationIsOpen_IsLeftToPeople(ConversationStatus status)
    {
        var harness = new Harness
        {
            HumanConversation = new MessagingConversation { ItemId = "thread", Status = status },
        };

        var started = await harness.Starter.TryStartAsync(Harness.Text(), Harness.Address, TestContext.Current.CancellationToken);

        Assert.False(started);
        Assert.Empty(harness.CreatedActivities);
    }

    [Fact]
    public async Task Text_AfterTheHumanConversationClosed_StartsTheAIConversation()
    {
        var harness = new Harness
        {
            HumanConversation = new MessagingConversation { ItemId = "thread", Status = ConversationStatus.Closed },
        };

        var started = await harness.Starter.TryStartAsync(Harness.Text(), Harness.Address, TestContext.Current.CancellationToken);

        Assert.True(started);
    }

    // A deleted profile, or one that is not a chat profile, cannot answer; the text reaches people rather than nobody.
    [Fact]
    public async Task Text_ToAnAIAgentWhoseProfileIsMissing_IsLeftToPeople()
    {
        var harness = new Harness { Profile = null };

        var started = await harness.Starter.TryStartAsync(Harness.Text(), Harness.Address, TestContext.Current.CancellationToken);

        Assert.False(started);
        Assert.Empty(harness.CreatedActivities);
    }

    [Fact]
    public async Task Text_FromAKnownContact_IsLinkedToTheContact()
    {
        var harness = new Harness { ContactItemId = "contact-1" };

        await harness.Starter.TryStartAsync(Harness.Text(), Harness.Address, TestContext.Current.CancellationToken);

        var activity = Assert.Single(harness.CreatedActivities);
        Assert.Equal("contact-1", activity.ContactContentItemId);
        Assert.Equal("Customer", activity.ContactContentType);
        Assert.Equal(ContactResolutionStatus.Resolved, activity.ContactResolutionStatus);
    }

    // The automated handler and the messaging workspace both ask about the same text in the same scope; the second asks
    // nothing of the stores and gets the first one's answer.
    [Fact]
    public async Task SecondAsk_ForTheSameTextInTheSameScope_ReusesTheAnswer()
    {
        var harness = new Harness();
        var text = Harness.Text();

        Assert.True(await harness.Starter.TryStartAsync(text, Harness.Address, TestContext.Current.CancellationToken));
        Assert.True(await harness.Starter.TryStartAsync(text, Harness.Address, TestContext.Current.CancellationToken));

        Assert.Single(harness.CreatedActivities);
        harness.RoutingResolver.Verify(resolver => resolver.ResolveAsync(It.IsAny<OmnichannelChannelEndpoint>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OutboundOrNonSmsMessages_StartNothing()
    {
        var harness = new Harness();

        var outbound = Harness.Text();
        outbound.IsInbound = false;

        var email = Harness.Text();
        email.Channel = OmnichannelConstants.Channels.Email;

        Assert.False(await harness.Starter.TryStartAsync(outbound, Harness.Address, TestContext.Current.CancellationToken));
        Assert.False(await harness.Starter.TryStartAsync(email, Harness.Address, TestContext.Current.CancellationToken));
        Assert.Empty(harness.CreatedActivities);
    }

    private sealed class Harness
    {
        public static readonly OmnichannelChannelEndpoint Address = new() { ItemId = AddressId, Value = "+18882720148", Capabilities = [OmnichannelConstants.Channels.Sms] };

        private SmsEntryPointAIConversationStarter _starter;

        public MessagingInboundRouting Routing { get; set; } = new() { AIProfileId = ProfileId, EntryPointId = "texts" };

        public AIProfile Profile { get; set; } = new() { ItemId = ProfileId, Type = AIProfileType.Chat };

        public OmnichannelActivity Latest { get; set; }

        public MessagingConversation HumanConversation { get; set; }

        public string ContactItemId { get; set; }

        public IReadOnlyList<SubjectFlowSettings> Flows { get; set; } = [];

        public List<OmnichannelActivity> CreatedActivities { get; } = [];

        public List<AIChatSession> SavedSessions { get; } = [];

        public Mock<ISession> Session { get; } = new();

        public Mock<IMessagingInboundRoutingResolver> RoutingResolver { get; } = new();

        public SmsEntryPointAIConversationStarter Starter => _starter ??= Create();

        public static OmnichannelMessage Text(string content = "Is the blue sedan still available?")
            => new()
            {
                Channel = OmnichannelConstants.Channels.Sms,
                IsInbound = true,
                CustomerAddress = Customer,
                ServiceAddress = Address.Value,
                Content = content,
            };

        private SmsEntryPointAIConversationStarter Create()
        {
            RoutingResolver
                .Setup(resolver => resolver.ResolveAsync(It.IsAny<OmnichannelChannelEndpoint>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Routing);

            var conversationStore = new Mock<IMessagingConversationStore>();
            conversationStore
                .Setup(store => store.FindByAddressesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => HumanConversation);

            var contactResolver = new Mock<IMessagingContactResolver>();
            contactResolver
                .Setup(resolver => resolver.ResolveContactContentItemIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult(ContactItemId));

            var activityStore = new Mock<IOmnichannelActivityStore>();
            activityStore
                .Setup(store => store.GetAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string>(), It.IsAny<ActivityInteractionType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CreatedActivities.LastOrDefault() ?? Latest);

            var activityManager = new Mock<IOmnichannelActivityManager>();
            activityManager
                .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new OmnichannelActivity { ItemId = "activity-" + CreatedActivities.Count });
            activityManager
                .Setup(manager => manager.CreateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()))
                .Callback((OmnichannelActivity activity, CancellationToken _) => CreatedActivities.Add(activity))
                .Returns(ValueTask.CompletedTask);

            var flows = new Mock<ISubjectFlowSettingsService>();
            flows
                .Setup(service => service.GetConfiguredFlowSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Flows);

            var profileManager = new Mock<IAIProfileManager>();
            profileManager
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Profile);

            var sessionManager = new Mock<IAIChatSessionManager>();
            sessionManager
                .Setup(manager => manager.SaveAsync(It.IsAny<AIChatSession>(), It.IsAny<CancellationToken>()))
                .Callback((AIChatSession session, CancellationToken _) => SavedSessions.Add(session))
                .Returns(Task.CompletedTask);

            var contentManager = new Mock<IContentManager>();
            contentManager
                .Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
                .ReturnsAsync((string id, VersionOptions _) => new ContentItem { ContentItemId = id, ContentType = "Customer" });
            contentManager
                .Setup(manager => manager.NewAsync(It.IsAny<string>()))
                .ReturnsAsync((string type) => new ContentItem { ContentType = type });

            var localLock = new Mock<ILocalLock>();
            localLock
                .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync((Mock.Of<ILocker>(), true));

            Session.Setup(session => session.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            return new SmsEntryPointAIConversationStarter(
                RoutingResolver.Object,
                MessagingTestChannels.Resolver(MessagingTestChannels.AcceptingDispatcher().Object),
                conversationStore.Object,
                contactResolver.Object,
                activityStore.Object,
                activityManager.Object,
                flows.Object,
                profileManager.Object,
                sessionManager.Object,
                contentManager.Object,
                localLock.Object,
                Session.Object,
                clock.Object,
                NullLogger<SmsEntryPointAIConversationStarter>.Instance);
        }
    }
}
