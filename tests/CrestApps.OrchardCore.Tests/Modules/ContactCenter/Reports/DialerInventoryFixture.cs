using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// A SQLite store with the real queue item, activity and interaction indexes, seeded with campaign work, that builds the
/// dialer queue inventory over it. Only what the classifier reads in full -- activity documents, work states, profiles and
/// suppression events -- is served from memory.
/// </summary>
internal sealed class DialerInventoryFixture : IAsyncDisposable
{
    public static readonly DateTime Now = new(2026, 10, 16, 15, 0, 0, DateTimeKind.Utc);

    private readonly Dictionary<string, OmnichannelActivity> _activities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DialerProfile> _profiles = new(StringComparer.Ordinal);
    private readonly List<QueueItem> _queueItems = [];
    private readonly List<Interaction> _interactions = [];
    private readonly IStore _store;
    private readonly string _databasePath;
    private int _sequence;

    private DialerInventoryFixture(IStore store, string databasePath)
    {
        _store = store;
        _databasePath = databasePath;
    }

    public IStore Store => _store;

    public static async Task<DialerInventoryFixture> CreateAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-dialer-inventory-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new QueueItemIndexProvider(), new InteractionIndexProvider(), new OmnichannelActivityIndexProvider()]);

        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);
        await store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, cancellationToken);

        await using (var session = store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

            var queueItemMigration = new QueueItemIndexMigrations(store, new StubClock(Now))
            {
                SchemaBuilder = schemaBuilder,
            };

            await queueItemMigration.CreateAsync();
            await queueItemMigration.UpdateFrom3Async();
            await queueItemMigration.UpdateFrom6Async();
            await InteractionQueryPlanFixture.MigrateAsync(store, transaction);
            await CreateActivityIndexAsync(schemaBuilder);
            await transaction.CommitAsync(cancellationToken);
        }

        return new DialerInventoryFixture(store, databasePath);
    }

    public void AddProfile(string profileId, string name, DialerMode mode, bool enabled)
    {
        _profiles[profileId] = new DialerProfile
        {
            ItemId = profileId,
            Name = name,
            Mode = mode,
            Enabled = enabled,
            MaxAttempts = 3,
        };
    }

    public OmnichannelActivity AddActivity(
        string campaignId,
        string dialerProfileId,
        ActivityStatus status = ActivityStatus.NotStated,
        string source = ActivitySources.PowerDial,
        DateTime? scheduledUtc = null,
        int attempts = 0,
        string assignedToId = null,
        DateTime? createdUtc = null)
    {
        var activity = new OmnichannelActivity
        {
            ItemId = $"act-{++_sequence:D4}",
            CampaignId = campaignId,
            DialerProfileId = dialerProfileId,
            Status = status,
            Source = source,
            Channel = OmnichannelConstants.Channels.Phone,
            InteractionType = ActivityInteractionType.Manual,
            ScheduledUtc = scheduledUtc ?? Now.AddHours(-2),
            CreatedUtc = createdUtc ?? Now.AddDays(-1),
            Attempts = attempts,
            AssignedToId = assignedToId,
            PreferredDestination = "+15550100" + _sequence.ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
        };

        _activities[activity.ItemId] = activity;

        return activity;
    }

    public QueueItem AddQueueItem(
        OmnichannelActivity activity,
        string queueId,
        string stampedProfileId,
        QueueItemStatus status = QueueItemStatus.Waiting,
        DateTime? enqueuedUtc = null,
        InteractionPriority priority = InteractionPriority.Normal)
    {
        var item = new QueueItem
        {
            ItemId = $"item-{++_sequence:D4}",
            QueueId = queueId,
            ActivityItemId = activity.ItemId,
            DialerProfileId = stampedProfileId,
            Priority = priority,
            EnqueuedUtc = enqueuedUtc ?? Now.AddMinutes(-30),
            QueueEnteredUtc = enqueuedUtc ?? Now.AddMinutes(-30),
        }.RestorePersistedStatus(status);

        _queueItems.Add(item);

        return item;
    }

    public void AddOutboundCall(string queueId, OmnichannelActivity activity, DateTime createdUtc)
    {
        _interactions.Add(new Interaction
        {
            ItemId = $"call-{++_sequence:D4}",
            QueueId = queueId,
            ActivityItemId = activity.ItemId,
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Outbound,
            CreatedUtc = createdUtc,
            EndedUtc = createdUtc.AddMinutes(1),
        }.RestorePersistedStatus(InteractionStatus.Ended));
    }

    public async Task SaveAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var session = _store.CreateSession();

        foreach (var activity in _activities.Values)
        {
            await session.SaveAsync(activity, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
        }

        foreach (var item in _queueItems)
        {
            await session.SaveAsync(item, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
        }

        foreach (var interaction in _interactions)
        {
            await session.SaveAsync(interaction, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public DialerQueueInventoryService CreateService(ISession session)
    {
        var profileReader = new Mock<IDialerProfileReader>();
        profileReader
            .Setup(reader => reader.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(_profiles.GetValueOrDefault(id)));

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.GetByIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) => ids
                .Where(_activities.ContainsKey)
                .Select(id => _activities[id])
                .ToArray());

        var workStates = new Mock<IContactCenterWorkStateStore>();
        workStates
            .Setup(store => store.GetByActivityIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContactCenterWorkState>());

        var events = new Mock<IInteractionEventStore>();
        events
            .Setup(store => store.GetByAggregateWindowAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InteractionEvent>());

        var queueItems = new Mock<IQueueItemManager>();
        queueItems
            .Setup(manager => manager.FindNextWaitingAsync(It.IsAny<ActivityQueue>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((ActivityQueue queue, DateTime _, CancellationToken cancellationToken) =>
                new QueueItemStore(session).FindNextWaitingAsync(queue.ItemId, cancellationToken));

        return new DialerQueueInventoryService(
            session,
            queueItems.Object,
            new DialerRecordClassifier(profileReader.Object, activityManager.Object, workStates.Object, events.Object, session),
            new StubClock(Now));
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    private static async Task CreateActivityIndexAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<ActivityKind>("Kind")
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("DialerProfileId", column => column.Nullable().WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledUtc", column => column.NotNull())
            .Column<DateTime>("CompletedUtc")
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("AssignedToUtc")
            .Column<ActivityAssignmentStatus>("AssignmentStatus")
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<ActivityUrgencyLevel>("UrgencyLevel")
            .Column<ActivityStatus>("Status")
            .Column<ActivityInteractionType>("InteractionType")
            .Column<bool>("AiEscalated")
            .Column<string>("TerminalReasonCode", column => column.Nullable().WithLength(64)),
            collection: OmnichannelConstants.CollectionName);
    }
}
