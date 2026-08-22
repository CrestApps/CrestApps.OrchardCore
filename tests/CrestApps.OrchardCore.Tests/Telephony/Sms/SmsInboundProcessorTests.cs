using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services.Routers;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.Notifications;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class SmsInboundProcessorTests
{
    [Fact]
    public async Task NewInbound_WithAgentRoute_CreatesAssignedConversation()
    {
        var routing = new SmsEndpointRoutingSettings { TargetType = SmsNumberRouteTargetType.Agent, TargetId = "agent-3" };
        var harness = new Harness(routing: routing);

        var message = harness.InboundMessage("Hi there");
        var conversation = await harness.Processor.ProcessAsync(message);

        Assert.NotNull(conversation);
        Assert.Equal(SmsConversationOwnerType.Personal, conversation.OwnerType);
        Assert.Equal("agent-3", conversation.AssignedAgentId);
        Assert.Equal(SmsConversationAssignmentStatus.Assigned, conversation.AssignmentStatus);
        Assert.Equal(1, conversation.UnreadCount);
        Assert.Equal(conversation.ItemId, message.ConversationId);
        Assert.NotNull(harness.CreatedConversation);
        harness.Notifier.Verify(n => n.NewInboundMessageAsync(It.IsAny<SmsInboundNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NewInbound_WithNoRoute_LandsInUnassignedInbox()
    {
        var harness = new Harness(routing: null);

        var conversation = await harness.Processor.ProcessAsync(harness.InboundMessage("hello"));

        Assert.NotNull(conversation);
        Assert.Equal(SmsConversationAssignmentStatus.Unassigned, conversation.AssignmentStatus);
        Assert.Null(conversation.AssignedAgentId);
    }

    [Fact]
    public async Task NewInbound_WhileAutomatedActivityActive_YieldsToTheAiPath()
    {
        var harness = new Harness(routing: null)
        {
            AutomatedActivity = new OmnichannelActivity { Status = ActivityStatus.AwaitingCustomerAnswer },
        };

        var conversation = await harness.Processor.ProcessAsync(harness.InboundMessage("hello"));

        Assert.Null(conversation);
        Assert.Null(harness.CreatedConversation);
    }

    [Fact]
    public async Task Inbound_OptOutKeyword_ClosesConversation()
    {
        var harness = new Harness(routing: null);

        var conversation = await harness.Processor.ProcessAsync(harness.InboundMessage("STOP"));

        Assert.NotNull(conversation);
        Assert.Equal(SmsConversationStatus.Closed, conversation.Status);
    }

    [Fact]
    public async Task ExistingConversation_AppendsAndKeepsAssignment()
    {
        var existing = new SmsConversation
        {
            ItemId = "conv-existing",
            ServiceAddress = "+15553334444",
            CustomerAddress = "+15551112222",
            OwnerType = SmsConversationOwnerType.Personal,
            OwnerId = "agent-owner",
            AssignedAgentId = "agent-owner",
            AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
            UnreadCount = 2,
        };

        var harness = new Harness(routing: null, existing: existing);

        var conversation = await harness.Processor.ProcessAsync(harness.InboundMessage("another"));

        Assert.Same(existing, conversation);
        Assert.Equal("agent-owner", conversation.AssignedAgentId);
        Assert.Equal(3, conversation.UnreadCount);
        Assert.Null(harness.CreatedConversation);
    }

    private sealed class Harness
    {
        public Mock<ISmsRealTimeNotifier> Notifier { get; } = new();

        public SmsConversation CreatedConversation { get; private set; }

        public OmnichannelActivity AutomatedActivity { get; set; }

        public SmsInboundProcessor Processor { get; }

        public Harness(SmsEndpointRoutingSettings routing, SmsConversation existing = null)
        {
            var endpoint = new OmnichannelChannelEndpoint { ItemId = "endpoint-1", Channel = "SMS", Value = "+15553334444" };

            if (routing is not null)
            {
                endpoint.Put(routing);
            }

            var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
            endpointManager.Setup(m => m.GetByServiceAddressAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(endpoint);

            var activityStore = new Mock<IOmnichannelActivityStore>();
            activityStore.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ActivityInteractionType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => AutomatedActivity);

            var conversationStore = new Mock<ISmsConversationStore>();
            conversationStore.Setup(s => s.FindByAddressesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            conversationStore.Setup(s => s.CreateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask)
                .Callback<SmsConversation, CancellationToken>((c, _) => CreatedConversation = c);
            conversationStore.Setup(s => s.UpdateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            var contactResolver = new Mock<ISmsContactResolver>();
            contactResolver.Setup(r => r.ResolveContactContentItemIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.FromResult<string>(null));

            var routers = new ISmsInboundRouter[]
            {
                new ExistingConversationRouter(),
                new NumberRouteRouter(),
                new FallbackRouter(),
            };

            var contentManager = new Mock<IContentManager>();

            var session = new Mock<ISession>();
            session.Setup(s => s.SaveAsync(It.IsAny<OmnichannelMessage>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

            Processor = new SmsInboundProcessor(
                endpointManager.Object,
                activityStore.Object,
                conversationStore.Object,
                contactResolver.Object,
                Notifier.Object,
                routers,
                contentManager.Object,
                session.Object,
                clock.Object,
                RedactorProviderFactory.Create(),
                NullLogger<SmsInboundProcessor>.Instance);
        }

        public OmnichannelMessage InboundMessage(string text)
            => new()
            {
                Channel = "SMS",
                ServiceAddress = "+15553334444",
                CustomerAddress = "+15551112222",
                Content = text,
                IsInbound = true,
                CreatedUtc = DateTime.UtcNow,
            };
    }
}
