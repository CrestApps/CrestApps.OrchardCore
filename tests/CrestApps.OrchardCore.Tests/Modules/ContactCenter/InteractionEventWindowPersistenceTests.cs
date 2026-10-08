using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A workforce report reads one period of the agent log and the state each agent was already in when it opened.
/// These run the real index, migrations and store against SQLite, so a window that leaks events from outside it, or
/// an opening state that is not the latest one, fails here rather than as a timecard that is quietly wrong.
/// </summary>
public sealed class InteractionEventWindowPersistenceTests
{
    private static readonly DateTime _from = new(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _through = _from.AddHours(8);

    private static readonly string[] _stateTypes =
    [
        ContactCenterConstants.Events.AgentStateChanged,
        ContactCenterConstants.Events.AgentPresenceChanged,
    ];

    [Fact]
    public async Task UpgradeStep_AddsTheAggregateIndex()
    {
        // Arrange
        var databasePath = DatabasePath("index");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            // Act
            await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name LIKE '%IDX_InteractionEventIndex_Aggregate%'";
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(1, count);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task Window_ReturnsOnlyThePeriodsEventsForTheAgentsAndTypesAsked_OldestFirst()
    {
        // Arrange
        var databasePath = DatabasePath("window");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(
                store,
                Event("before", "agent-1", _from.AddTicks(-1), ContactCenterConstants.Events.AgentStateChanged),
                Event("opening", "agent-1", _from, ContactCenterConstants.Events.AgentStateChanged),
                Event("later", "agent-1", _from.AddHours(2), ContactCenterConstants.Events.AgentPresenceChanged),
                Event("earlier", "agent-1", _from.AddHours(1), ContactCenterConstants.Events.AgentStateChanged),
                Event("closing", "agent-1", _through, ContactCenterConstants.Events.AgentStateChanged),
                Event("after", "agent-1", _through.AddTicks(1), ContactCenterConstants.Events.AgentStateChanged),
                Event("other-type", "agent-1", _from.AddHours(3), ContactCenterConstants.Events.AgentEntitlementsChanged),
                Event("other-agent", "agent-2", _from.AddHours(3), ContactCenterConstants.Events.AgentStateChanged),
                Event("other-aggregate", "agent-1", _from.AddHours(3), ContactCenterConstants.Events.AgentStateChanged, aggregateType: nameof(Interaction)));

            await using var session = store.CreateSession();
            var eventStore = CreateEventStore(session);

            // Act
            var events = await eventStore.GetByAggregateWindowAsync(nameof(AgentProfile), _stateTypes, ["agent-1"], _from, _through, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["opening", "earlier", "later", "closing"], events.Select(interactionEvent => interactionEvent.ItemId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task Window_WithoutAgents_ReturnsEveryAgentsEventsInThePeriod()
    {
        // Arrange
        var databasePath = DatabasePath("window-all");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(
                store,
                Event("agent-1-event", "agent-1", _from.AddHours(1), ContactCenterConstants.Events.AgentStateChanged),
                Event("agent-2-event", "agent-2", _from.AddHours(2), ContactCenterConstants.Events.AgentStateChanged),
                Event("agent-2-before", "agent-2", _from.AddHours(-2), ContactCenterConstants.Events.AgentStateChanged));

            await using var session = store.CreateSession();

            // Act
            var events = await CreateEventStore(session).GetByAggregateWindowAsync(nameof(AgentProfile), _stateTypes, null, _from, _through, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["agent-1-event", "agent-2-event"], events.Select(interactionEvent => interactionEvent.ItemId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task Window_WithMoreAgentsThanOneBatch_StillFindsEveryAgentsEvents()
    {
        // Arrange
        var databasePath = DatabasePath("window-batches");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(
                store,
                Event("first", "agent-0000", _from.AddHours(1), ContactCenterConstants.Events.AgentStateChanged),
                Event("middle", "agent-0600", _from.AddHours(2), ContactCenterConstants.Events.AgentStateChanged),
                Event("last", "agent-1199", _from.AddHours(3), ContactCenterConstants.Events.AgentStateChanged));

            var agentIds = Enumerable.Range(0, 1200).Select(index => $"agent-{index:0000}").ToArray();

            await using var session = store.CreateSession();

            // Act
            var events = await CreateEventStore(session).GetByAggregateWindowAsync(nameof(AgentProfile), _stateTypes, agentIds, _from, _through, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["first", "middle", "last"], events.Select(interactionEvent => interactionEvent.ItemId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LatestBefore_ReturnsEachAgentsLastStateChangeBeforeThePeriod()
    {
        // Arrange
        var databasePath = DatabasePath("latest-before");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(
                store,
                Event("agent-1-old", "agent-1", _from.AddDays(-3), ContactCenterConstants.Events.AgentStateChanged),
                Event("agent-1-latest", "agent-1", _from.AddMinutes(-5), ContactCenterConstants.Events.AgentStateChanged),
                Event("agent-1-not-state", "agent-1", _from.AddMinutes(-1), ContactCenterConstants.Events.AgentEntitlementsChanged),
                Event("agent-1-at-open", "agent-1", _from, ContactCenterConstants.Events.AgentStateChanged),
                Event("agent-2-legacy", "agent-2", _from.AddDays(-40), ContactCenterConstants.Events.AgentPresenceChanged),
                Event("agent-3-in-period", "agent-3", _from.AddHours(1), ContactCenterConstants.Events.AgentStateChanged));

            await using var session = store.CreateSession();

            // Act
            var anchors = await CreateEventStore(session).GetLatestBeforeAsync(
                nameof(AgentProfile),
                _stateTypes,
                ["agent-1", "agent-2", "agent-3", "agent-unknown"],
                _from,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["agent-2-legacy", "agent-1-latest"], anchors.Select(interactionEvent => interactionEvent.ItemId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LatestBefore_WhenTwoChangesShareAnInstant_ReturnsTheOneWrittenLast()
    {
        // Arrange
        var databasePath = DatabasePath("latest-tie");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, Event("written-first", "agent-1", _from.AddMinutes(-5), ContactCenterConstants.Events.AgentStateChanged));
            await SeedAsync(store, Event("written-second", "agent-1", _from.AddMinutes(-5), ContactCenterConstants.Events.AgentStateChanged));

            await using var session = store.CreateSession();

            // Act
            var anchors = await CreateEventStore(session).GetLatestBeforeAsync(nameof(AgentProfile), _stateTypes, ["agent-1"], _from, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("written-second", Assert.Single(anchors).ItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static InteractionEventStore CreateEventStore(ISession session)
        => new(session, new DefaultInteractionEventUpcastService([]));

    private static InteractionEvent Event(string id, string agentId, DateTime occurredUtc, string eventType, string aggregateType = nameof(AgentProfile))
        => new()
        {
            ItemId = id,
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = agentId,
            OccurredUtc = occurredUtc,
            IdempotencyKey = id,
        };

    private static async Task SeedAsync(IStore store, params InteractionEvent[] events)
    {
        await using var session = store.CreateSession();

        foreach (var interactionEvent in events)
        {
            await session.SaveAsync(interactionEvent, collection: ContactCenterStorage.CollectionName);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new InteractionEventIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new InteractionEventIndexMigrations(store)
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        };

        // The full upgrade path a new tenant runs, so the step that adds the aggregate index runs here too.
        var version = await migration.CreateAsync();
        Assert.Equal(2, version);
        Assert.Equal(3, await migration.UpdateFrom2Async());
        Assert.Equal(4, await migration.UpdateFrom3Async());

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static string DatabasePath(string name)
        => Path.Combine(Path.GetTempPath(), $"cc-event-window-{name}-{Guid.NewGuid():N}.db");
}
