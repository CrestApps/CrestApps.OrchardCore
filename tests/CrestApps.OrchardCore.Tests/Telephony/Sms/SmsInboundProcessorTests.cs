using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Notifications;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class SmsInboundProcessorTests
{
    [Fact]
    public async Task NewInbound_WithAgentRoute_CreatesAssignedConversation()
    {
        var routing = new SmsEndpointRoutingSettings { TargetType = SmsNumberRouteTargetType.Agent, TargetId = "agent-3" };
        var harness = new Harness(routing: routing);

        var message = Harness.InboundMessage("Hi there");
        var conversation = await harness.Processor.ProcessAsync(message, TestContext.Current.CancellationToken);

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

        var conversation = await harness.Processor.ProcessAsync(Harness.InboundMessage("hello"), TestContext.Current.CancellationToken);

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

        var conversation = await harness.Processor.ProcessAsync(Harness.InboundMessage("hello"), TestContext.Current.CancellationToken);

        Assert.Null(conversation);
        Assert.Null(harness.CreatedConversation);
    }

    [Theory]
    [InlineData(ActivityStatus.Completed)]
    [InlineData(ActivityStatus.Cancelled)]
    [InlineData(ActivityStatus.Failed)]
    [InlineData(ActivityStatus.Purged)]
    public async Task ProcessAsync_WhenAutomatedActivityIsTerminal_CreatesHumanConversation(ActivityStatus status)
    {
        // A finished automated activity - however it finished - must not keep the number locked away from the
        // human inbox, otherwise one failed AI turn silently drops every later text from that contact.
        var harness = new Harness(routing: null)
        {
            AutomatedActivity = new OmnichannelActivity { Status = status },
        };

        var conversation = await harness.Processor.ProcessAsync(Harness.InboundMessage("hello"), TestContext.Current.CancellationToken);

        Assert.NotNull(conversation);
        Assert.NotNull(harness.CreatedConversation);
    }

    [Fact]
    public async Task Inbound_OptOutKeyword_ClosesConversation()
    {
        var harness = new Harness(routing: null);

        var conversation = await harness.Processor.ProcessAsync(Harness.InboundMessage("STOP"), TestContext.Current.CancellationToken);

        Assert.NotNull(conversation);
        Assert.Equal(SmsConversationStatus.Closed, conversation.Status);
    }

    [Theory]
    [InlineData("Yes")]
    [InlineData("Yes.")]
    [InlineData("START")]
    public async Task Inbound_OptInKeyword_WhenTheContactIsNotOptedOut_SendsNothing(string body)
    {
        // "YES" is an opt-in keyword AND the most ordinary answer there is: an automated agent opens by asking a
        // yes/no question. Answering it must not be read as a resubscribe, because the confirmation would land as a
        // second, unrelated message on top of the agent's real reply — which is exactly the duplicate a customer
        // sees as the bot texting twice. With nothing to opt back into, there is nothing to confirm.
        var harness = new Harness(routing: null, contactContentItemId: "contact-1", contactOptedOut: false);

        var conversation = await harness.Processor.ProcessAsync(Harness.InboundMessage(body), TestContext.Current.CancellationToken);

        Assert.NotNull(conversation);
        Assert.NotEqual(SmsConversationStatus.Closed, conversation.Status);
        harness.Dispatcher.Verify(
            d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Inbound_OptInKeyword_WhenTheContactIsOptedOut_ConfirmsTheResubscribe()
    {
        // The keyword still has to work for the person it exists for: someone who opted out and is asking to be
        // reachable again gets the confirmation, and the thread is reopened so their next message keeps its history.
        var harness = new Harness(routing: null, contactContentItemId: "contact-1", contactOptedOut: true);

        var conversation = await harness.Processor.ProcessAsync(Harness.InboundMessage("YES"), TestContext.Current.CancellationToken);

        Assert.NotNull(conversation);
        Assert.Equal(SmsConversationStatus.Open, conversation.Status);
        harness.Dispatcher.Verify(
            d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Inbound_OptOutKeyword_AlwaysConfirms_EvenWhenTheContactIsNotOptedOut()
    {
        // STOP and HELP are unconditional: the carrier rules require an answer however the conversation is going,
        // so the gate that quiets a redundant opt-in must never quiet these.
        var harness = new Harness(routing: null, contactContentItemId: "contact-1", contactOptedOut: false);

        await harness.Processor.ProcessAsync(Harness.InboundMessage("STOP"), TestContext.Current.CancellationToken);

        harness.Dispatcher.Verify(
            d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExistingConversation_AppendsAndKeepsAssignment()
    {
        var existing = new SmsConversation
        {
            ItemId = "conv-existing",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = SmsConversationOwnerType.Personal,
            OwnerId = "agent-owner",
            AssignedAgentId = "agent-owner",
            AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
            UnreadCount = 2,
        };

        var harness = new Harness(routing: null, existing: existing);

        var conversation = await harness.Processor.ProcessAsync(Harness.InboundMessage("another"), TestContext.Current.CancellationToken);

        Assert.Same(existing, conversation);
        Assert.Equal("agent-owner", conversation.AssignedAgentId);
        Assert.Equal(3, conversation.UnreadCount);
        Assert.Null(harness.CreatedConversation);
    }

    [Fact]
    public async Task ProcessAsync_SerializesTheThread_OnTheAddressPairLock()
    {
        // A per-thread lock is what stops two texts arriving in the same second from creating two conversations
        // for one number pair.
        var harness = new Harness(routing: null);

        await harness.Processor.ProcessAsync(Harness.InboundMessage("hello"), TestContext.Current.CancellationToken);

        Assert.Contains("SmsConversation:+15553334444:+15551112222", harness.DistributedLock.AcquiredKeys);
    }

    [Fact]
    public async Task ProcessAsync_WhenTheThreadLockIsNotAcquired_Throws()
    {
        // Dropping the message would lose it. Throwing lets the durable provider inbox retry the delivery.
        var harness = new Harness(routing: null, lockAcquired: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Processor.ProcessAsync(Harness.InboundMessage("hello"), TestContext.Current.CancellationToken));

        Assert.Null(harness.CreatedConversation);
    }

    [Fact]
    public async Task ProcessAsync_WhenAnotherNodeCreatedTheThreadFirst_ReReadsItInsteadOfCreatingASecond()
    {
        // The unique index on the address pair is the last line of defence when two nodes race past the lock.
        // Losing that race must land the message on the winner's thread, not fail the delivery.
        var winner = new SmsConversation
        {
            ItemId = "conv-winner",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = SmsConversationOwnerType.Personal,
            AssignmentStatus = SmsConversationAssignmentStatus.Unassigned,
        };

        var harness = new Harness(routing: null, createConflictsWith: winner);

        var conversation = await harness.Processor.ProcessAsync(Harness.InboundMessage("hello"), TestContext.Current.CancellationToken);

        Assert.Same(winner, conversation);
        Assert.Equal(1, winner.UnreadCount);
    }

    private sealed class Harness
    {
        public Mock<ISmsRealTimeNotifier> Notifier { get; } = new();

        public FakeDistributedLock DistributedLock { get; } = new();

        public SmsConversation CreatedConversation { get; private set; }

        public OmnichannelActivity AutomatedActivity { get; set; }

        public SmsInboundProcessor Processor { get; }

        /// <summary>
        /// The dispatcher the keyword confirmations go through, so a test can assert one was (or was not) sent.
        /// </summary>
        public Mock<ISmsDispatcher> Dispatcher { get; } = new();

        public Harness(
            SmsEndpointRoutingSettings routing,
            SmsConversation existing = null,
            bool lockAcquired = true,
            SmsConversation createConflictsWith = null,
            string contactContentItemId = null,
            bool contactOptedOut = false)
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
            var reads = 0;

            conversationStore.Setup(s => s.FindByAddressesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    reads++;

                    // The first read misses (the thread does not exist yet); once the conflicting create has been
                    // observed, the second read finds the row the other node committed.
                    return createConflictsWith is not null && reads > 1
                        ? createConflictsWith
                        : existing;
                });

            if (createConflictsWith is null)
            {
                conversationStore.Setup(s => s.CreateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()))
                    .Returns(ValueTask.CompletedTask)
                    .Callback<SmsConversation, CancellationToken>((c, _) => CreatedConversation = c);
            }
            else
            {
                conversationStore.Setup(s => s.CreateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()))
                    .Returns(() => ValueTask.FromException(new InvalidOperationException("UNIQUE constraint failed: UQ_SmsConversationIndex_Addresses")));
            }

            conversationStore.Setup(s => s.UpdateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            var contactResolver = new Mock<ISmsContactResolver>();
            contactResolver.Setup(r => r.ResolveContactContentItemIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult(contactContentItemId));

            var routers = new ISmsInboundRouter[]
            {
                new ExistingConversationRouter(),
                new NumberRouteRouter(),
                new FallbackRouter(),
            };

            var contentManager = new Mock<IContentManager>();

            if (contactContentItemId is not null)
            {
                var contact = new ContentItem { ContentType = "Customer", ContentItemId = contactContentItemId };
                contact.Alter<OmnichannelContactPart>(part => part.SetDoNotSms(contactOptedOut, DateTime.UtcNow));

                contentManager.Setup(m => m.GetAsync(contactContentItemId, It.IsAny<VersionOptions>()))
                    .ReturnsAsync(contact);
            }

            var session = new Mock<ISession>();
            session.Setup(s => s.SaveAsync(It.IsAny<OmnichannelMessage>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

            IDistributedLock distributedLock = lockAcquired
                ? DistributedLock
                : new RefusingDistributedLock();

            Processor = new SmsInboundProcessor(
                endpointManager.Object,
                activityStore.Object,
                conversationStore.Object,
                contactResolver.Object,
                Notifier.Object,
                new SmsConversationRouter(routers, NullLogger<SmsConversationRouter>.Instance),
                new NoOpSmsFirstResponseSlaService(),
                Dispatcher.Object,
                new OptionsWrapper<SmsKeywordReplySettings>(new SmsKeywordReplySettings()),
                contentManager.Object,
                distributedLock,
                new OptionsWrapper<SmsPortalOptions>(new SmsPortalOptions()),
                session.Object,
                clock.Object,
                RedactorProviderFactory.Create(),
                NullLogger<SmsInboundProcessor>.Instance);
        }

        public static OmnichannelMessage InboundMessage(string text)
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

    // Never grants the lock, standing in for a node that is already processing this thread.
    private sealed class RefusingDistributedLock : IDistributedLock
    {
        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => Task.FromResult<ILocker>(null);

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
            => Task.FromResult<(ILocker, bool)>((null, false));

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(true);
    }
}
