using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Messaging.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// An automated (AI) SMS conversation that escalates to a person reaches the portal thread two ways: the inbound
/// pipeline sees every text the customer sends, and the handoff copies the automated transcript across. Both used
/// to write, so every message the AI handled showed twice in the thread the agent inherited. These run the real
/// inbound processor and the real handoff against one SQLite database and pin the invariant: each customer and
/// AI message appears in the thread exactly once, with the provider's message id kept wherever it was known.
/// </summary>
public sealed class SmsHandoffTranscriptTests
{
    private const string ServiceAddress = "+15553334444";
    private const string ContactAddress = "+15551112222";
    private const string QueueId = "queue-1";

    private static readonly DateTime _start = new(2026, 9, 25, 17, 38, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InboundDuringAnAutomatedActivity_ThenHandoff_RecordsEachMessageOnce(bool processorRunsFirst)
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();

        // The contact had texted the number before the automated conversation started, so the portal thread
        // already exists when the AI takes over.
        var conversationId = await harness.SeedConversationAsync("Hi, I need help with my account", "SM-before", _start);
        harness.Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "activity-1", AISessionId = "session-1", Status = ActivityStatus.AwaitingCustomerAnswer };

        // Act
        // The customer answers the AI. The automated handler replies; the portal must not record it a second time.
        await harness.DeliverAsync(Inbound("Yes", "SM-yes", _start.AddMinutes(14)));

        // The customer asks for a person. The automated handler hands off (copying its transcript) and concludes
        // the activity; the inbound processor runs in the same delivery, before or after it.
        var trigger = Inbound("I would like to speak to someone please", "SM-talk", _start.AddMinutes(22));
        var transcript = new[]
        {
            Turn("prompt-1", isInbound: false, "Hi, do you have a minute to talk about your next vehicle?", _start.AddMinutes(1)),
            Turn("prompt-2", isInbound: true, "Yes", _start.AddMinutes(14), "SM-yes"),
            Turn("prompt-3", isInbound: false, "Great! New or used?", _start.AddMinutes(15)),
            Turn("prompt-4", isInbound: true, "I would like to speak to someone please", _start.AddMinutes(22), "SM-talk"),
            Turn("prompt-5", isInbound: false, "Connecting you with a specialist now.", _start.AddMinutes(23)),
        };

        await harness.DeliverAsync(trigger, handoffTranscript: transcript, processorRunsFirst: processorRunsFirst);

        // A person has the thread now, so the next text lands in it directly.
        await harness.DeliverAsync(Inbound("Sure, let's do it", "SM-after", _start.AddMinutes(24)));

        // Assert
        var thread = await harness.GetThreadAsync(conversationId);

        Assert.Equal(
            [
                "Hi, I need help with my account",
                "Hi, do you have a minute to talk about your next vehicle?",
                "Yes",
                "Great! New or used?",
                "I would like to speak to someone please",
                "Connecting you with a specialist now.",
                "Sure, let's do it",
            ],
            thread.Select(message => message.Content).ToArray());

        Assert.Equal("SM-yes", thread.Single(message => message.Content == "Yes").ProviderMessageId);
        Assert.Equal("SM-talk", thread.Single(message => message.Content == "I would like to speak to someone please").ProviderMessageId);
    }

    [Fact]
    public async Task RedeliveredWebhook_WithTheSameProviderMessageId_IsRecordedOnce()
    {
        // Arrange
        // Twilio and Telnyx both deliver at least once. The in-process claim gate forgets on a restart and does
        // not span nodes, so the thread itself has to recognise a message it already holds.
        await using var harness = await Harness.CreateAsync();
        var conversationId = await harness.SeedConversationAsync("Hello", "SM-first", _start);

        // Act
        await harness.DeliverAsync(Inbound("Is anyone there?", "SM-retried", _start.AddMinutes(1)));
        await harness.DeliverAsync(Inbound("Is anyone there?", "SM-retried", _start.AddMinutes(1)));

        // Assert
        var thread = await harness.GetThreadAsync(conversationId);

        Assert.Equal(["Hello", "Is anyone there?"], thread.Select(message => message.Content).ToArray());
    }

    [Fact]
    public async Task RedeliveredHandoffMessage_AfterHandoff_IsRecordedOnce()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        var conversationId = await harness.SeedConversationAsync("Hello", "SM-first", _start);
        harness.Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "activity-1", AISessionId = "session-1", Status = ActivityStatus.AwaitingCustomerAnswer };

        var trigger = Inbound("Agent please", "SM-talk", _start.AddMinutes(2));

        await harness.DeliverAsync(trigger, handoffTranscript:
        [
            Turn("prompt-1", isInbound: true, "Agent please", _start.AddMinutes(2), "SM-talk"),
            Turn("prompt-2", isInbound: false, "Connecting you now.", _start.AddMinutes(3)),
        ]);

        // Act
        await harness.DeliverAsync(Inbound("Agent please", "SM-talk", _start.AddMinutes(2)));

        // Assert
        var thread = await harness.GetThreadAsync(conversationId);

        Assert.Single(thread, message => message.Content == "Agent please");
    }

    [Fact]
    public async Task ReplayedHandoff_DoesNotImportTheTranscriptTwice()
    {
        // Arrange
        // A handoff that is retried (a re-driven turn, a second escalation of the same session) carries the same
        // transcript again. The AI's own replies have no provider id, so the copy is keyed by the transcript
        // entry it came from rather than by its text or time.
        await using var harness = await Harness.CreateAsync();
        var transcript = new[]
        {
            Turn("prompt-1", isInbound: false, "Hi there", _start),
            Turn("prompt-2", isInbound: true, "Hi", _start.AddMinutes(1), "SM-hi"),
            Turn("prompt-3", isInbound: false, "Hi there", _start.AddMinutes(2)),
        };

        harness.Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "activity-1", AISessionId = "session-1", Status = ActivityStatus.AwaitingCustomerAnswer };

        // Act
        await harness.HandOffAsync(transcript);
        await harness.HandOffAsync(transcript);

        // Assert
        var conversationId = await harness.FindConversationIdAsync();
        var thread = await harness.GetThreadAsync(conversationId);

        // The AI legitimately said "Hi there" twice; both stay, once each.
        Assert.Equal(["Hi there", "Hi", "Hi there"], thread.Select(message => message.Content).ToArray());
    }

    [Fact]
    public async Task Handoff_SkipsACustomerTextTheThreadAlreadyHolds_ByItsProviderMessageId()
    {
        // Arrange
        // The text that opened the automated conversation was recorded live before the activity existed, and a
        // redelivery left the same text in the automated transcript twice. Neither copy may be imported again.
        await using var harness = await Harness.CreateAsync();
        var conversationId = await harness.SeedConversationAsync("Do you have the blue one?", "SM-open", _start);
        harness.Activity = new OmnichannelActivity { Channel = "SMS", ItemId = "activity-1", AISessionId = "session-1", Status = ActivityStatus.AwaitingCustomerAnswer };

        // Act
        await harness.HandOffAsync(
        [
            Turn("prompt-1", isInbound: true, "Do you have the blue one?", _start, "SM-open"),
            Turn("prompt-2", isInbound: false, "We do! Want me to hold it?", _start.AddMinutes(1)),
            Turn("prompt-3", isInbound: true, "Yes please", _start.AddMinutes(2), "SM-yes"),
            Turn("prompt-4", isInbound: true, "Yes please", _start.AddMinutes(2), "SM-yes"),
        ]);

        // Assert
        var thread = await harness.GetThreadAsync(conversationId);

        Assert.Equal(
            ["Do you have the blue one?", "We do! Want me to hold it?", "Yes please"],
            thread.Select(message => message.Content).ToArray());
    }

    private static OmnichannelMessage Inbound(string content, string providerMessageId, DateTime createdUtc)
        => new()
        {
            Channel = OmnichannelConstants.Channels.Sms,
            ServiceAddress = ServiceAddress,
            CustomerAddress = ContactAddress,
            Content = content,
            IsInbound = true,
            CreatedUtc = createdUtc,
            ProviderMessageId = providerMessageId,
        };

    private static OmnichannelHandoffMessage Turn(string id, bool isInbound, string content, DateTime createdUtc, string providerMessageId = null)
        => new()
        {
            Id = id,
            IsInbound = isInbound,
            Content = content,
            CreatedUtc = createdUtc,
            ProviderMessageId = providerMessageId,
        };

    private sealed class Harness : IAsyncDisposable
    {
        private readonly IStore _store;
        private readonly string _databasePath;
        private readonly FakeDistributedLock _distributedLock = new();
        private readonly OmnichannelChannelEndpoint _endpoint;

        private Harness(IStore store, string databasePath)
        {
            _store = store;
            _databasePath = databasePath;
            _endpoint = new OmnichannelChannelEndpoint
            {
                ItemId = "endpoint-1",
                Channel = OmnichannelConstants.Channels.Sms,
                Value = ServiceAddress,
            };
        }

        /// <summary>
        /// Gets or sets the automated activity on the number, as the activity store would return it.
        /// </summary>
        public OmnichannelActivity Activity { get; set; }

        public static async Task<Harness> CreateAsync()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var databasePath = Path.Combine(Path.GetTempPath(), $"sms-handoff-{Guid.NewGuid():N}.db");
            var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

            store.RegisterIndexes([new MessagingConversationIndexProvider()], MessagingStorage.CollectionName);
            store.RegisterIndexes([new OmnichannelMessageIndexProvider()], OmnichannelConstants.CollectionName);
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(MessagingStorage.CollectionName, cancellationToken);
            await store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, cancellationToken);

            await using (var migrationSession = store.CreateSession())
            {
                var transaction = await migrationSession.BeginTransactionAsync(cancellationToken);
                var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

                await schemaBuilder.CreateMapIndexTableAsync<MessagingConversationIndex>(table => table
                    .Column<string>("ItemId", column => column.WithLength(26))
                    .Column<string>("Channel", column => column.WithLength(MessagingStorage.ChannelLength))
                    .Column<string>("ServiceAddress", column => column.WithLength(MessagingStorage.AddressLength))
                    .Column<string>("ContactAddress", column => column.WithLength(MessagingStorage.AddressLength))
                    .Column<string>("ContactContentItemId", column => column.WithLength(26))
                    .Column<string>("CustomerKey", column => column.WithLength(MessagingStorage.CustomerKeyLength))
                    .Column<string>("OwnerType", column => column.WithLength(32))
                    .Column<string>("OwnerId", column => column.WithLength(26))
                    .Column<string>("AssignedAgentId", column => column.WithLength(26))
                    .Column<string>("AssignmentStatus", column => column.WithLength(32))
                    .Column<string>("Status", column => column.WithLength(32))
                    .Column<bool>("IsRead")
                    .Column<DateTime>("LastMessageUtc")
                    .Column<int>("UnreadCount", column => column.NotNull().WithDefault(0))
                    .Column<DateTime>("AssignedUtc", column => column.Nullable())
                    .Column<DateTime>("FirstResponseDueUtc", column => column.Nullable()),
                    collection: MessagingStorage.CollectionName);

                await schemaBuilder.CreateMapIndexTableAsync<OmnichannelMessageIndex>(table => table
                    .Column<string>("Channel", column => column.WithLength(50))
                    .Column<string>("CustomerAddress", column => column.WithLength(255))
                    .Column<string>("ServiceAddress", column => column.WithLength(255))
                    .Column<DateTime>("CreatedUtc", column => column.NotNull())
                    .Column<bool>("IsInbound", column => column.NotNull().WithDefault(false))
                    .Column<string>("ConversationId", column => column.WithLength(26))
                    .Column<string>("ProviderMessageId", column => column.WithLength(128)),
                    collection: OmnichannelConstants.CollectionName);

                await transaction.CommitAsync(cancellationToken);
            }

            return new Harness(store, databasePath);
        }

        public async Task<string> SeedConversationAsync(string content, string providerMessageId, DateTime createdUtc)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var conversation = new MessagingConversation
            {
                ItemId = UniqueId.GenerateId(),
                Channel = OmnichannelConstants.Channels.Sms,
                ServiceAddress = ServiceAddress,
                ContactAddress = ContactAddress,
                Status = ConversationStatus.Open,
                OwnerType = ConversationOwnerType.Personal,
                AssignmentStatus = ConversationAssignmentStatus.Unassigned,
                CreatedUtc = createdUtc,
            };

            await using var session = _store.CreateSession();
            await new MessagingConversationStore(session).CreateAsync(conversation, cancellationToken);

            var message = Inbound(content, providerMessageId, createdUtc);
            message.ConversationId = conversation.ItemId;

            await session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            return conversation.ItemId;
        }

        /// <summary>
        /// Plays one provider delivery the way the webhook does: an audit row with no conversation, then every
        /// event handler in one scope. The automated handler is stood in for by the handoff it performs.
        /// </summary>
        public async Task DeliverAsync(
            OmnichannelMessage message,
            IReadOnlyList<OmnichannelHandoffMessage> handoffTranscript = null,
            bool processorRunsFirst = false)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var session = _store.CreateSession();

            var audit = Inbound(message.Content, message.ProviderMessageId, message.CreatedUtc);
            await session.SaveAsync(audit, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            var processor = CreateProcessor(session);

            if (processorRunsFirst)
            {
                await processor.ProcessAsync(message, cancellationToken);
            }

            if (handoffTranscript is not null)
            {
                await HandOffAsync(session, handoffTranscript);
            }

            if (!processorRunsFirst)
            {
                await processor.ProcessAsync(message, cancellationToken);
            }

            await session.SaveChangesAsync(cancellationToken);
        }

        public async Task HandOffAsync(IReadOnlyList<OmnichannelHandoffMessage> transcript)
        {
            await using var session = _store.CreateSession();

            await HandOffAsync(session, transcript);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task<string> FindConversationIdAsync()
        {
            await using var session = _store.CreateSession();

            var conversation = await new MessagingConversationStore(session)
                .FindByAddressesAsync("SMS", ServiceAddress, ContactAddress, TestContext.Current.CancellationToken);

            return conversation.ItemId;
        }

        public async Task<IReadOnlyList<OmnichannelMessage>> GetThreadAsync(string conversationId)
        {
            await using var session = _store.CreateSession();

            return (await session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                    index => index.ConversationId == conversationId,
                    collection: OmnichannelConstants.CollectionName)
                .OrderBy(index => index.CreatedUtc)
                .ThenBy(index => index.Id)
                .ListAsync(TestContext.Current.CancellationToken))
                .ToArray();
        }

        public ValueTask DisposeAsync()
        {
            TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

            return ValueTask.CompletedTask;
        }

        private async Task HandOffAsync(ISession session, IReadOnlyList<OmnichannelHandoffMessage> transcript)
        {
            var result = await CreateHandoffService(session).RequestHandoffAsync(
                new OmnichannelHandoffRequest
                {
                    Activity = Activity,
                    TargetQueueId = QueueId,
                    ServiceAddress = ServiceAddress,
                    ContactAddress = ContactAddress,
                    Transcript = transcript,
                },
                TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded, result.Message);

            // The automated handler concludes the activity once the handoff succeeds.
            Activity.Status = ActivityStatus.Completed;
            Activity.TerminalReasonCode = OmnichannelConstants.TerminalReasons.HandedOffToAgent;
        }

        private MessagingInboundProcessor CreateProcessor(ISession session)
        {
            var clock = CreateClock();
            var activityStore = new Mock<IOmnichannelActivityStore>();
            activityStore
                .Setup(store => store.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ActivityInteractionType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Activity);

            var contactResolver = new Mock<IMessagingContactResolver>();
            contactResolver
                .Setup(resolver => resolver.ResolveContactContentItemIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult<string>(null));

            return new MessagingInboundProcessor(
                Channels,
                CreateEndpointManager(),
                activityStore.Object,
                new MessagingConversationStore(session),
                contactResolver.Object,
                Mock.Of<IMessagingRealTimeNotifier>(),
                CreateRouter(clock),
                new NoOpSmsFirstResponseSlaService(),
                [],
                _distributedLock,
                new OptionsWrapper<MessagingWorkspaceOptions>(new MessagingWorkspaceOptions()),
                session,
                clock,
                RedactorProviderFactory.Create(),
                NullLogger<MessagingInboundProcessor>.Instance);
        }

        private MessagingAgentHandoffService CreateHandoffService(ISession session)
        {
            var clock = CreateClock();
            var queuePolicyReader = new Mock<IMessagingQueuePolicyReader>();
            queuePolicyReader
                .Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MessagingQueuePolicy(true, 0, null));

            return new MessagingAgentHandoffService(
                Channels,
                new MessagingConversationStore(session),
                Mock.Of<IMessagingRealTimeNotifier>(),
                queuePolicyReader.Object,
                CreateEndpointManager(),
                CreateRouter(clock),
                session,
                clock,
                NullLogger<MessagingAgentHandoffService>.Instance);
        }

        private IOmnichannelChannelEndpointManager CreateEndpointManager()
        {
            var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
            endpointManager
                .Setup(manager => manager.GetByServiceAddressAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_endpoint);

            return endpointManager.Object;
        }

        // The SMS channel over a dispatcher that accepts everything, so the pipeline runs as it does on a tenant with SMS.
        private static IMessagingChannelResolver Channels { get; } = MessagingTestChannels.Resolver(MessagingTestChannels.AcceptingDispatcher().Object);

        private static MessagingConversationRouter CreateRouter(IClock clock)
            => new(
                [
                    new ExistingConversationRouter(),
                    new EndpointRouteRouter(),
                    new HandoffQueueRouter(Mock.Of<IMessagingRoutingStrategy>(), clock),
                    new FallbackRouter(),
                ],
                Channels,
                NullLogger<MessagingConversationRouter>.Instance);

        private static IClock CreateClock()
        {
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_start.AddHours(1));

            return clock.Object;
        }
    }
}
