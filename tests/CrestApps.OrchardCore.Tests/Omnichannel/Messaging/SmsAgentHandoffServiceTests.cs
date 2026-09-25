using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;

using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public class SmsAgentHandoffServiceTests
{
    [Fact]
    public void CanHandle_OnlySms()
    {
        var harness = new Harness();

        Assert.True(harness.Service.CanHandle("SMS"));
        Assert.True(harness.Service.CanHandle("sms"));
        Assert.False(harness.Service.CanHandle("Phone"));
    }

    [Fact]
    public async Task RequestHandoff_CreatesQueueOwnedConversation_ImportsTranscript_AndNotifies()
    {
        var harness = new Harness();

        var request = new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "act1", AISessionId = "sess-1", ContactContentItemId = "contact-1" },
            TargetQueueId = "queue-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            Transcript =
            [
                new OmnichannelHandoffMessage { IsInbound = false, Content = "Hi, are you still shopping?", CreatedUtc = DateTime.UtcNow.AddMinutes(-5) },
                new OmnichannelHandoffMessage { IsInbound = true, Content = "Yes, can I talk to a person?", CreatedUtc = DateTime.UtcNow.AddMinutes(-4) },
                new OmnichannelHandoffMessage { IsInbound = false, Content = "Connecting you now.", CreatedUtc = DateTime.UtcNow.AddMinutes(-3) },
            ],
        };

        var result = await harness.Service.RequestHandoffAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        var conversation = harness.CreatedConversation;
        Assert.NotNull(conversation);
        Assert.Equal(result.ConversationId, conversation.ItemId);
        Assert.Equal(ConversationOwnerType.Queue, conversation.OwnerType);
        Assert.Equal("queue-1", conversation.OwnerId);
        Assert.Equal(ConversationAssignmentStatus.Unassigned, conversation.AssignmentStatus);
        Assert.Null(conversation.AssignedAgentId);
        Assert.Equal(ConversationStatus.Open, conversation.Status);
        Assert.Equal("sess-1", conversation.AISessionId);
        Assert.Equal("contact-1", conversation.ContactContentItemId);
        Assert.False(conversation.IsRead);
        Assert.Equal(3, conversation.UnreadCount);
        Assert.Equal("Connecting you now.", conversation.LastMessagePreview);

        // The three transcript turns were imported as linked messages.
        Assert.Equal(3, harness.SavedMessages.Count);
        Assert.All(harness.SavedMessages, m => Assert.Equal(conversation.ItemId, m.ConversationId));
        Assert.Contains(harness.SavedMessages, m => m.IsInbound && m.Content == "Yes, can I talk to a person?");
        Assert.Contains(harness.SavedMessages, m => !m.IsInbound && m.Content == "Hi, are you still shopping?");

        harness.Notifier.Verify(n => n.NewInboundMessageAsync(
            It.Is<MessagingInboundNotification>(x => x.ConversationId == conversation.ItemId && x.OwnerQueueId == "queue-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestHandoff_StoresSummary_OnTheConversation()
    {
        var harness = new Harness();

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "act1", AISessionId = "s1" },
            TargetQueueId = "queue-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            Summary = "Customer wants a refund on a damaged order and asked for a person.",
            Reason = "customer asked for a human",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("Customer wants a refund on a damaged order and asked for a person.", harness.CreatedConversation.Summary);
    }

    [Fact]
    public async Task RequestHandoff_WithoutSummary_FallsBackToReason()
    {
        var harness = new Harness();

        await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "act1" },
            TargetQueueId = "queue-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            Summary = null,
            Reason = "customer asked for a human",
        }, TestContext.Current.CancellationToken);

        Assert.Equal("customer asked for a human", harness.CreatedConversation.Summary);
    }

    [Fact]
    public async Task RequestHandoff_WithExistingThread_ReRoutesToQueue_WithoutCreating()
    {
        var existing = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-existing",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Personal,
            OwnerId = "agent-owner",
            AssignedAgentId = "agent-owner",
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
            Status = ConversationStatus.Closed,
            UnreadCount = 1,
        };

        var harness = new Harness(existing);

        var request = new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "act1", AISessionId = "sess-9" },
            TargetQueueId = "queue-2",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
        };

        var result = await harness.Service.RequestHandoffAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("conv-existing", result.ConversationId);
        Assert.Null(harness.CreatedConversation);
        Assert.Same(existing, harness.UpdatedConversation);
        Assert.Equal(ConversationOwnerType.Queue, existing.OwnerType);
        Assert.Equal("queue-2", existing.OwnerId);
        Assert.Equal(ConversationAssignmentStatus.Unassigned, existing.AssignmentStatus);
        Assert.Null(existing.AssignedAgentId);
        // A closed thread is re-opened for the queue.
        Assert.Equal(ConversationStatus.Open, existing.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequestHandoff_WithoutQueue_Fails(string queueId)
    {
        var harness = new Harness();

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "act1" },
            TargetQueueId = queueId,
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(harness.CreatedConversation);
    }

    [Fact]
    public async Task RequestHandoff_WithoutAddresses_Fails()
    {
        var harness = new Harness();

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "act1" },
            TargetQueueId = "queue-1",
            ServiceAddress = null,
            ContactAddress = null,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(harness.CreatedConversation);
    }


    [Fact]
    public async Task RequestHandoff_RefusesAQueueThatDoesNotExist()
    {
        // Arrange
        // A misconfigured escalation queue used to produce a conversation owned by an identifier nothing
        // resolves: it is in no agent's inbox, no supervisor sees it under a department, and the customer is
        // waiting for a reply from a queue that does not exist.
        var harness = new Harness(queueExists: false);

        // Act
        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "act1" },
            TargetQueueId = "missing-queue",
            ServiceAddress = "+16502530000",
            ContactAddress = "+16502530001",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("queue", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(harness.CreatedConversation);
    }

    [Fact]
    public async Task RequestHandoff_OnARoutedQueue_PushAssignsToTheSelectedAgent()
    {
        // Arrange
        // A routed queue exists precisely so a thread lands on one person rather than in a pool nobody owns.
        // Escalations bypassed that and pooled every handoff, so a routed department silently behaved like a
        // shared one for exactly the conversations that needed an owner most.
        var harness = new Harness(distributionMode: ConversationDistributionMode.Routed, selectedAgentId: "agent-7");

        // Act
        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "act1" },
            TargetQueueId = "queue-1",
            ServiceAddress = "+16502530000",
            ContactAddress = "+16502530001",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("agent-7", harness.CreatedConversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Assigned, harness.CreatedConversation.AssignmentStatus);
        Assert.Equal(ConversationOwnerType.Queue, harness.CreatedConversation.OwnerType);
        Assert.Equal("queue-1", harness.CreatedConversation.OwnerId);
    }

    [Fact]
    public async Task RequestHandoff_OnARoutedQueueWithNobodyAvailable_FallsBackToTheSharedPool()
    {
        // Arrange
        // Refusing the handoff because nobody is free would strand the customer in an automated thread that has
        // already given up on them. The pool is the correct fallback: the thread is visible to the department.
        var harness = new Harness(distributionMode: ConversationDistributionMode.Routed, selectedAgentId: null);

        // Act
        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "act1" },
            TargetQueueId = "queue-1",
            ServiceAddress = "+16502530000",
            ContactAddress = "+16502530001",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(harness.CreatedConversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Unassigned, harness.CreatedConversation.AssignmentStatus);
        Assert.Equal("queue-1", harness.CreatedConversation.OwnerId);
    }

    [Fact]
    public async Task RequestHandoff_OnASharedPoolQueue_NeverConsultsTheRoutingStrategy()
    {
        // Arrange
        // A shared pool is a deliberate choice, not an absence of one, so it must not be quietly turned into a
        // push assignment by the escalation path.
        var harness = new Harness(distributionMode: ConversationDistributionMode.SharedPool, selectedAgentId: "agent-7");

        // Act
        await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "act1" },
            TargetQueueId = "queue-1",
            ServiceAddress = "+16502530000",
            ContactAddress = "+16502530001",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(harness.CreatedConversation.AssignedAgentId);
        harness.RoutingStrategy.Verify(
            strategy => strategy.SelectAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestHandoff_NotifiesTheAssignedAgent_WhenTheThreadWasPushAssigned()
    {
        // Arrange
        // The notification is how the agent learns the thread exists. Sending it with no assignee, when one was
        // chosen, lights up the whole department for a conversation only one person can act on.
        var harness = new Harness(distributionMode: ConversationDistributionMode.Routed, selectedAgentId: "agent-7");

        // Act
        await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "act1" },
            TargetQueueId = "queue-1",
            ServiceAddress = "+16502530000",
            ContactAddress = "+16502530001",
        }, TestContext.Current.CancellationToken);

        // Assert
        harness.Notifier.Verify(
            notifier => notifier.NewInboundMessageAsync(
                It.Is<MessagingInboundNotification>(notification => notification.AssignedAgentId == "agent-7" && notification.OwnerQueueId == "queue-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class Harness
    {
        public Mock<IMessagingRealTimeNotifier> Notifier { get; } = new();

        public MessagingConversation CreatedConversation { get; private set; }

        public MessagingConversation UpdatedConversation { get; private set; }

        public List<OmnichannelMessage> SavedMessages { get; } = [];

        public Mock<IMessagingRoutingStrategy> RoutingStrategy { get; } = new();

        public MessagingAgentHandoffService Service { get; }

        public Harness(
            MessagingConversation existing = null,
            bool queueExists = true,
            ConversationDistributionMode distributionMode = ConversationDistributionMode.SharedPool,
            string selectedAgentId = null)
        {
            var conversationStore = new Mock<IMessagingConversationStore>();
            conversationStore.Setup(s => s.FindByAddressesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            conversationStore.Setup(s => s.CreateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask)
                .Callback<MessagingConversation, CancellationToken>((c, _) => CreatedConversation = c);
            conversationStore.Setup(s => s.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask)
                .Callback<MessagingConversation, CancellationToken>((c, _) => UpdatedConversation = c);

            var session = new Mock<ISession>();
            session.Setup(s => s.SaveAsync(It.IsAny<OmnichannelMessage>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<object, bool, string, CancellationToken>((m, _, _, _) => SavedMessages.Add((OmnichannelMessage)m));

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

            var queuePolicyReader = new Mock<IMessagingQueuePolicyReader>();
            queuePolicyReader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(queueExists ? new MessagingQueuePolicy(true, 0, null) : MessagingQueuePolicy.NotFound);

            var endpoint = new OmnichannelChannelEndpoint
            {
                ItemId = "endpoint-1",
                Channel = OmnichannelConstants.Channels.Sms,
                Value = "+16502530000",
            };

            endpoint.Put(new MessagingEndpointRoutingSettings
            {
                TargetType = ConversationRouteTargetType.Queue,
                TargetId = "queue-1",
                DistributionMode = distributionMode,
            });

            var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
            endpointManager
                .Setup(manager => manager.GetByServiceAddressAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(endpoint);

            RoutingStrategy
                .Setup(strategy => strategy.SelectAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(selectedAgentId);

            var router = new MessagingConversationRouter(
                [new HandoffQueueRouter(RoutingStrategy.Object, clock.Object)],
                MessagingTestChannels.Resolver(MessagingTestChannels.AcceptingDispatcher().Object),
                NullLogger<MessagingConversationRouter>.Instance);

            Service = new MessagingAgentHandoffService(
                MessagingTestChannels.Resolver(MessagingTestChannels.AcceptingDispatcher().Object),
                conversationStore.Object,
                Notifier.Object,
                queuePolicyReader.Object,
                endpointManager.Object,
                router,
                session.Object,
                clock.Object,
                NullLogger<MessagingAgentHandoffService>.Instance);
        }
    }
}
