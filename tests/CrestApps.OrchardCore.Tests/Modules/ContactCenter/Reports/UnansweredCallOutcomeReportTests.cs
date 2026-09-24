using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// Two direct-to-agent calls were reported the wrong way round. One rang its agent, went unanswered and was sent to
/// voicemail; the reports counted it as abandoned, with its greeting and recording in the wait. The other was
/// queued while the only agent was on a break, offered the moment they came back, and the caller hung up half a
/// second later; its interaction settled as failed, so the reports left it out of the abandons and counted a
/// technical failure that never happened. These run the reports over a real store holding one call of each kind a
/// caller can end without talking to anybody, seeded as the platform writes them.
/// </summary>
public sealed class UnansweredCallOutcomeReportTests
{
    private const string QueueId = "queue-1";
    private const string QueueName = "Support";

    private static readonly DateTime _from = new(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _to = new(2026, 9, 24, 23, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime _t0 = new(2026, 9, 24, 21, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task QueueAbandonment_CountsCallersWhoHungUpAsAbandonedAndCallsSentToVoicemailApart()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateSeededStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();

            // Act
            var document = await RunEnterpriseAsync(session, EnterpriseInteractionReportKind.QueueAbandonment, cancellationToken);

            // Assert
            var section = Assert.Single(document.Sections);
            var row = Assert.Single(section.Rows, value => value.Cells[0] == QueueName);
            string Cell(string column) => row.Cells[section.Columns.ToList().FindIndex(value => value.Label == column)];

            Assert.Equal("8", Cell("Offered"));
            Assert.Equal("1", Cell("Answered"));
            Assert.Equal("3", Cell("Abandoned"));
            Assert.Equal(ReportFormat.Percent(3d / 8), Cell("Abandonment rate"));

            // From joining the queue to hanging up: 41s, 20s and 30s.
            Assert.Equal(ReportFormat.Duration(91d / 3), Cell("Avg wait before abandon"));
            Assert.Equal("2", Cell("Voicemail"));

            // From joining the queue to being sent to voicemail, without the greeting or the message: 73s and 60s.
            Assert.Equal(ReportFormat.Duration(133d / 2), Cell("Avg wait before voicemail"));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task AbandonedAndFailedDetail_ListEachCallUnderTheOutcomeItHad()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateSeededStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();

            // Act
            var abandoned = await RunEnterpriseAsync(session, EnterpriseInteractionReportKind.AbandonedInteractionDetail, cancellationToken);
            var failed = await RunEnterpriseAsync(session, EnterpriseInteractionReportKind.FailedInteractionDetail, cancellationToken);

            // Assert
            Assert.Equal(
                ["hung-up-queued", "hung-up-ringing", "hung-up-ringing-stored-failed"],
                InteractionIds(abandoned));
            Assert.Equal(["provider-failed"], InteractionIds(failed));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task ExecutiveSummary_AndCallInsights_AgreeOnEveryOutcome()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateSeededStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();

            // Act
            var summary = await RunEnterpriseAsync(session, EnterpriseInteractionReportKind.ExecutiveSummary, cancellationToken);
            var insights = await CreateReportingService(session).GetCallInsightsAsync(_from, _to, cancellationToken);

            // Assert
            var metrics = summary.Sections.First(section => section.Kind == ReportSectionKind.Metrics).Metrics
                .ToDictionary(metric => metric.Label, metric => metric.Value);
            Assert.Equal("1", metrics["Inbound answered"]);
            Assert.Equal("3", metrics["Abandoned"]);
            Assert.Equal("2", metrics["Voicemail"]);
            Assert.Equal("1", metrics["Failed"]);

            Assert.Equal(1, insights.Answered);
            Assert.Equal(3, insights.Abandoned);
            Assert.Equal(2, insights.Voicemail);
            Assert.Equal(1, insights.Failed);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static string[] InteractionIds(ReportDocument document)
        => [.. Assert.Single(document.Sections).Rows.Select(row => row.Cells[1]).Order(StringComparer.Ordinal)];

    private static async Task<ReportDocument> RunEnterpriseAsync(
        ISession session,
        EnterpriseInteractionReportKind kind,
        CancellationToken cancellationToken)
    {
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ActivityQueue>)[new ActivityQueue { ItemId = QueueId, Name = QueueName }]);
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<AgentProfile>)[new AgentProfile { ItemId = "agent-1", UserName = "agent" }]);
        var guard = new Mock<IContactCenterReportCapabilityGuard>();
        guard
            .Setup(value => value.GetMissingFeaturesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)[]);

        var definition = new EnterpriseInteractionReportDefinition(
            kind.ToString(),
            () => new LocalizedString(kind.ToString(), kind.ToString()),
            () => new LocalizedString(kind.ToString(), kind.ToString()),
            kind,
            "Interactions",
            []);

        var provider = new EnterpriseInteractionReportProvider(
            session,
            CreateEventStore(session),
            queueManager.Object,
            agentManager.Object,
            definition,
            guard.Object,
            new PassThroughStringLocalizer<EnterpriseInteractionReportProvider>(),
            TimeSpan.FromDays(400));

        var filter = new ReportFilter();
        filter.SetDateRange(new ReportDateRange { FromUtc = _from, ToUtc = _to });

        return await provider.RunAsync(new ReportContext(filter), cancellationToken);
    }

    private static ContactCenterReportingService CreateReportingService(ISession session)
    {
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ActivityQueue>)[new ActivityQueue { ItemId = QueueId, Name = QueueName }]);

        return new ContactCenterReportingService(
            session,
            new Mock<IActivityQueueGroupManager>().Object,
            queueManager.Object,
            new Mock<IQueueItemManager>().Object,
            new Mock<IAgentProfileManager>().Object,
            new Mock<ICatalogManager<OmnichannelCampaign>>().Object,
            new Mock<ICatalogManager<OmnichannelCampaignGroup>>().Object,
            Options.Create(new ContactCenterReportingOptions()),
            CreateEventStore(session));
    }

    private static InteractionEventStore CreateEventStore(ISession session)
        => new(session, new DefaultInteractionEventUpcastService([]));

    private static async Task<(IStore Store, string DatabasePath)> CreateSeededStoreAsync(CancellationToken cancellationToken)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-unanswered-outcomes-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new InteractionIndexProvider(), new InteractionEventIndexProvider()]);

        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);

        await using (var migrationSession = store.CreateSession())
        {
            var transaction = await migrationSession.BeginTransactionAsync(cancellationToken);
            await InteractionQueryPlanFixture.MigrateAsync(store, transaction);

            var eventMigration = new InteractionEventIndexMigrations(store)
            {
                SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
            };
            await eventMigration.CreateAsync();
            await eventMigration.UpdateFrom2Async();
            await eventMigration.UpdateFrom3Async();
            await transaction.CommitAsync(cancellationToken);
        }

        await using var session = store.CreateSession();
        var seed = new Seed(session);

        // An agent was offered the call and the caller hung up while it rang, as the reports now store it.
        await seed.InteractionAsync("hung-up-ringing", InteractionStatus.Ended, created: 0, ended: 20.5, agentId: "agent-1");
        await seed.QueuedAsync("hung-up-ringing", at: 0.5);
        await seed.LeftQueueAsync("hung-up-ringing", at: 20.5, waited: 20, state: "Removed");
        await seed.AbandonedAsync("hung-up-ringing", at: 20.5, waited: 20);

        // The same, stored before the fix: the caller's cancel settled the interaction as failed.
        await seed.InteractionAsync("hung-up-ringing-stored-failed", InteractionStatus.Failed, created: 100, ended: 141.5, agentId: "agent-1");
        await seed.QueuedAsync("hung-up-ringing-stored-failed", at: 100.5);
        await seed.LeftQueueAsync("hung-up-ringing-stored-failed", at: 141.5, waited: 41, state: "Removed");
        await seed.AbandonedAsync("hung-up-ringing-stored-failed", at: 141.5, waited: 41);

        // Queued with nobody available, and the caller gave up.
        await seed.InteractionAsync("hung-up-queued", InteractionStatus.Ended, created: 200, ended: 231);
        await seed.QueuedAsync("hung-up-queued", at: 201);
        await seed.LeftQueueAsync("hung-up-queued", at: 231, waited: 30, state: "Removed");
        await seed.AbandonedAsync("hung-up-queued", at: 231, waited: 30);

        // The offer went unanswered and the caller was sent to voicemail; the greeting and the message took the call on
        // past the voicemail, and the recording arrived later still.
        await seed.InteractionAsync("unanswered-offer-voicemail", InteractionStatus.Ended, created: 300, ended: 388, voicemail: true);
        await seed.QueuedAsync("unanswered-offer-voicemail", at: 300);
        await seed.LeftQueueAsync("unanswered-offer-voicemail", at: 373, waited: 73, state: "Removed");
        await seed.SentToVoicemailAsync("unanswered-offer-voicemail", at: 406);

        // The caller waited as long as the queue allows and went to voicemail. The platform answered the leg to record
        // the message, which the provider reports as an answer.
        await seed.InteractionAsync("max-wait-voicemail", InteractionStatus.Ended, created: 400, ended: 520, answered: 461, voicemail: true);
        await seed.QueuedAsync("max-wait-voicemail", at: 400);
        await seed.LeftQueueAsync("max-wait-voicemail", at: 460, waited: 60, state: "Removed");
        await seed.SentToVoicemailAsync("max-wait-voicemail", at: 460);

        // An agent answered, and the caller hung up at the end of the conversation.
        await seed.InteractionAsync("answered", InteractionStatus.Ended, created: 500, ended: 570, answered: 510, agentId: "agent-1");
        await seed.QueuedAsync("answered", at: 500);
        await seed.LeftQueueAsync("answered", at: 510, waited: 10, state: "Assigned");

        // The provider could not carry the call.
        await seed.InteractionAsync("provider-failed", InteractionStatus.Failed, created: 600, ended: 601);

        // Still waiting when the report ran.
        await seed.InteractionAsync("still-waiting", InteractionStatus.Created, created: 700, ended: null);
        await seed.QueuedAsync("still-waiting", at: 700);

        await session.SaveChangesAsync(cancellationToken);

        return (store, databasePath);
    }

    private sealed class Seed
    {
        private readonly ISession _session;
        private int _sequence;

        public Seed(ISession session)
        {
            _session = session;
        }

        public Task InteractionAsync(
            string id,
            InteractionStatus status,
            double created,
            double? ended,
            double? answered = null,
            string agentId = null,
            bool voicemail = false)
        {
            var interaction = new Interaction
            {
                ItemId = id,
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Inbound,
                QueueId = QueueId,
                AgentId = agentId,
                CreatedUtc = _t0.AddSeconds(created),
                AnsweredUtc = answered.HasValue ? _t0.AddSeconds(answered.Value) : null,
                EndedUtc = ended.HasValue ? _t0.AddSeconds(ended.Value) : null,
            }.RestorePersistedStatus(status);

            if (voicemail)
            {
                interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.ProjectionMetadataKey] = true;
            }

            return _session.SaveAsync(interaction, collection: ContactCenterStorage.CollectionName);
        }

        public Task QueuedAsync(string id, double at)
            => EventAsync(id, ContactCenterConstants.Events.CallQueued, at, new CallLifecycleEventData { QueueId = QueueId, State = "Waiting" });

        public Task LeftQueueAsync(string id, double at, double waited, string state)
            => EventAsync(id, ContactCenterConstants.Events.CallDequeued, at, new CallLifecycleEventData { QueueId = QueueId, State = state, DurationSeconds = waited });

        public Task AbandonedAsync(string id, double at, double waited)
            => EventAsync(id, ContactCenterConstants.Events.CallAbandoned, at, new CallLifecycleEventData { QueueId = QueueId, Reason = CallLifecycleReasons.CallerHungUp, DurationSeconds = waited });

        public Task SentToVoicemailAsync(string id, double at)
            => EventAsync(id, ContactCenterConstants.Events.CallSentToVoicemail, at, data: null);

        private Task EventAsync(string id, string eventType, double at, CallLifecycleEventData data)
        {
            var interactionEvent = new InteractionEvent
            {
                ItemId = $"event-{++_sequence}",
                InteractionId = id,
                EventType = eventType,
                AggregateType = nameof(Interaction),
                AggregateId = id,
                OccurredUtc = _t0.AddSeconds(at),
                RecordedUtc = _t0.AddSeconds(at),
                SchemaVersion = ContactCenterStorage.CurrentEventSchemaVersion,
                IdempotencyKey = $"{eventType}:{id}:{_sequence}",
            };

            if (data is not null)
            {
                data.InteractionId = id;
                interactionEvent.SetData(data);
            }

            return _session.SaveAsync(interactionEvent, collection: ContactCenterStorage.CollectionName);
        }
    }
}
