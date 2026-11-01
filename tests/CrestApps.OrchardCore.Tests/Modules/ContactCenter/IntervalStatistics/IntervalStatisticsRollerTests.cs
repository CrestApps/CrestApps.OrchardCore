using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.IntervalStatistics;

/// <summary>
/// The interval statistics roller on a real SQLite store: it computes today and backfills earlier days, refreshes
/// today on its schedule, resumes a rebuild one bounded step at a time, and deletes days past the retention window.
/// </summary>
public sealed class IntervalStatisticsRollerTests : IAsyncLifetime
{
    private static readonly DateTime _today = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), "interval-statistics-" + Guid.NewGuid().ToString("N") + ".db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([
            new InteractionIndexProvider(),
            new InteractionEventIndexProvider(),
            new IntervalStatisticsDayIndexProvider(),
            new QueueIntervalStatisticsIndexProvider(),
            new AgentIntervalStatisticsIndexProvider(),
        ]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await InteractionQueryPlanFixture.MigrateAsync(_store, transaction);

        var events = new InteractionEventIndexMigrations(_store) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) };
        await events.CreateAsync();
        await events.UpdateFrom2Async();
        await events.UpdateFrom3Async();
        await events.UpdateFrom6Async();

        await new IntervalStatisticsIndexMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task FirstRun_ComputesTodayAndBackfillsToTheFirstInteraction()
    {
        await SeedAsync(
            Inbound("a", _today.AddDays(-2).AddHours(9), answeredAfter: 20, talk: 300),
            Inbound("b", _today.AddDays(-1).AddHours(14).AddMinutes(20), answeredAfter: 90, talk: 120),
            Inbound("c", _today.AddHours(8).AddMinutes(5), answeredAfter: null, talk: 0));
        await SeedEventsAsync(SignIn("agent-1", _today.AddHours(8)));

        var clock = new AdvanceableClock(_today.AddHours(10));
        var changes = await RollAsync(clock);

        Assert.Equal(3, changes);

        await using var session = _store.CreateSession();
        var reader = new IntervalStatisticsReader(session);
        var days = await reader.GetDaysAsync(_today.AddDays(-5), _today.AddDays(1), TestContext.Current.CancellationToken);

        Assert.Equal([_today.AddDays(-2), _today.AddDays(-1), _today], days.Select(day => DateTime.SpecifyKind(day.Date, DateTimeKind.Utc)));
        Assert.True(days[1].Final);
        Assert.False(days[2].Final);

        var intervals = await reader.GetQueueIntervalsAsync(["sales"], _today.AddDays(-2), _today.AddDays(1), TestContext.Current.CancellationToken);

        Assert.Equal(3, intervals.Count);
        Assert.Equal(_today.AddDays(-1).AddHours(14).AddMinutes(15), intervals[1].IntervalStartUtc);
        Assert.Equal(1, intervals[2].Abandoned);

        var agent = await reader.GetAgentIntervalsAsync(["user-1"], _today, _today.AddDays(1), TestContext.Current.CancellationToken);

        // Signed in at 8:00 and still signed in at 10:00: eight intervals of 15 minutes.
        Assert.Equal(8, agent.Count);
        Assert.All(agent, interval => Assert.Equal(900, interval.SignedInSeconds));
    }

    [Fact]
    public async Task ASiteWithoutHistory_ComputesOnlyToday()
    {
        var clock = new AdvanceableClock(_today.AddHours(10));

        Assert.Equal(1, await RollAsync(clock));
        Assert.Equal(0, await RollAsync(clock));

        await using var session = _store.CreateSession();
        var days = await new IntervalStatisticsReader(session).GetDaysAsync(_today.AddDays(-30), _today.AddDays(1), TestContext.Current.CancellationToken);

        Assert.Equal(_today, DateTime.SpecifyKind(Assert.Single(days).Date, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Today_IsComputedAgainOnlyOnItsRefresh()
    {
        await SeedAsync(Inbound("a", _today.AddHours(8), answeredAfter: 10, talk: 60));

        var clock = new AdvanceableClock(_today.AddHours(9));

        Assert.Equal(1, await RollAsync(clock));
        Assert.Equal(0, await RollAsync(clock));

        await SeedAsync(Inbound("b", _today.AddHours(9).AddMinutes(1), answeredAfter: 10, talk: 60));
        clock.Advance(TimeSpan.FromMinutes(20));

        Assert.Equal(1, await RollAsync(clock));

        await using var session = _store.CreateSession();
        var intervals = await new IntervalStatisticsReader(session).GetQueueIntervalsAsync(null, _today, _today.AddDays(1), TestContext.Current.CancellationToken);

        Assert.Equal(2, intervals.Sum(interval => interval.Offered));
    }

    [Fact]
    public async Task Rebuild_ResumesOneBoundedStepAtATime()
    {
        await SeedAsync(
            Inbound("a", _today.AddDays(-3).AddHours(9), answeredAfter: 20, talk: 300),
            Inbound("b", _today.AddHours(8), answeredAfter: 20, talk: 300));

        var clock = new AdvanceableClock(_today.AddHours(10));

        // Bring every day up to date first, so the runs below only rebuild.
        while (await RollAsync(clock) > 0)
        {
        }

        await using (var session = _store.CreateSession())
        {
            var roller = CreateRoller(session, clock, maxDaysPerRun: 1);
            await roller.RequestRebuildAsync(_today.AddDays(-3), _today.AddDays(-1), "admin", TestContext.Current.CancellationToken);
        }

        var cursors = new List<DateTime?>();

        for (var run = 0; run < 3; run++)
        {
            Assert.Equal(1, await RollAsync(clock, maxDaysPerRun: 1));

            await using var session = _store.CreateSession();
            var state = await CreateRoller(session, clock).GetStateAsync(TestContext.Current.CancellationToken);
            cursors.Add(state.RebuildNextUtc);
        }

        Assert.Equal([_today.AddDays(-2), _today.AddDays(-1), null], cursors.Select(cursor => cursor.HasValue ? DateTime.SpecifyKind(cursor.Value, DateTimeKind.Utc) : (DateTime?)null));
        Assert.Equal(0, await RollAsync(clock, maxDaysPerRun: 1));
    }

    [Fact]
    public async Task ARunOfSeveralDays_SavesTheStateAfterEachDayAndAtTheEnd()
    {
        await SeedAsync(Inbound("a", _today.AddDays(-3).AddHours(9), answeredAfter: 20, talk: 300));

        var clock = new AdvanceableClock(_today.AddHours(10));

        while (await RollAsync(clock) > 0)
        {
        }

        await using (var session = _store.CreateSession())
        {
            await CreateRoller(session, clock).RequestRebuildAsync(_today.AddDays(-3), _today, "admin", TestContext.Current.CancellationToken);
        }

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(4, await RollAsync(clock));

        await using (var session = _store.CreateSession())
        {
            var state = await CreateRoller(session, clock).GetStateAsync(TestContext.Current.CancellationToken);

            Assert.False(state.IsRebuilding);
            Assert.Null(state.RebuildNextUtc);
            Assert.Equal(clock.UtcNow, state.LastRunUtc);
        }

        Assert.Equal(0, await RollAsync(clock));
    }

    [Fact]
    public async Task DaysPastTheRetentionWindow_AreDeleted()
    {
        await SeedAsync(
            Inbound("a", _today.AddDays(-3).AddHours(9), answeredAfter: 20, talk: 300),
            Inbound("b", _today.AddHours(8), answeredAfter: 20, talk: 300));

        var clock = new AdvanceableClock(_today.AddHours(10));

        while (await RollAsync(clock) > 0)
        {
        }

        await RollAsync(clock, retentionDays: 1);

        await using var session = _store.CreateSession();
        var days = await new IntervalStatisticsReader(session).GetDaysAsync(_today.AddDays(-10), _today.AddDays(1), TestContext.Current.CancellationToken);

        Assert.Equal([_today.AddDays(-1), _today], days.Select(day => DateTime.SpecifyKind(day.Date, DateTimeKind.Utc)));
    }

    private async Task<int> RollAsync(AdvanceableClock clock, int maxDaysPerRun = 5, int retentionDays = 760)
    {
        await using var session = _store.CreateSession();

        return await CreateRoller(session, clock, maxDaysPerRun, retentionDays).RollAsync(TestContext.Current.CancellationToken);
    }

    private static IntervalStatisticsRoller CreateRoller(ISession session, AdvanceableClock clock, int maxDaysPerRun = 5, int retentionDays = 760)
    {
        var queues = new Mock<IActivityQueueManager>();
        queues.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActivityQueue { ItemId = "sales", Name = "Sales", SlaThresholdSeconds = 60 }]);

        var agents = new Mock<IAgentProfileManager>();
        agents.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentProfile { ItemId = "agent-1", UserId = "user-1", UserName = "agent1" }]);

        return new IntervalStatisticsRoller(
            session,
            new InteractionEventStore(session, new DefaultInteractionEventUpcastService([])),
            queues.Object,
            agents.Object,
            Options.Create(new IntervalStatisticsOptions
            {
                BackfillDays = 30,
                RetentionDays = retentionDays,
                MaxDaysPerRun = maxDaysPerRun,
            }),
            clock,
            NullLogger<IntervalStatisticsRoller>.Instance);
    }

    private async Task SeedAsync(params Interaction[] interactions)
    {
        await using var session = _store.CreateSession();

        foreach (var interaction in interactions)
        {
            await session.SaveAsync(interaction, collection: ContactCenterStorage.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedEventsAsync(params InteractionEvent[] events)
    {
        await using var session = _store.CreateSession();

        foreach (var interactionEvent in events)
        {
            await session.SaveAsync(interactionEvent, collection: ContactCenterStorage.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Interaction Inbound(string id, DateTime created, int? answeredAfter, int talk)
    {
        var answered = answeredAfter.HasValue ? created.AddSeconds(answeredAfter.Value) : (DateTime?)null;

        return new Interaction
        {
            ItemId = id,
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            QueueId = "sales",
            AgentId = answered.HasValue ? "agent-1" : null,
            CreatedUtc = created,
            AnsweredUtc = answered,
            EndedUtc = (answered ?? created).AddSeconds(answered.HasValue ? talk : 30),
        }.RestorePersistedStatus(answered.HasValue ? InteractionStatus.Ended : InteractionStatus.Ended);
    }

    private static InteractionEvent SignIn(string agentId, DateTime at)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-" + agentId + at.Ticks,
            EventType = ContactCenterConstants.Events.AgentStateChanged,
            AggregateType = nameof(AgentProfile),
            AggregateId = agentId,
            OccurredUtc = at,
            RecordedUtc = at,
            ActorType = ContactCenterActorType.Agent,
        };

        interactionEvent.SetData(new AgentStateChangedEventData
        {
            AgentId = agentId,
            PreviousState = AgentPresenceStatus.Offline,
            CurrentState = AgentPresenceStatus.Available,
            Source = AgentStateChangeSources.SignIn,
            QueueIds = ["sales"],
            CampaignIds = [],
            ChangedUtc = at,
        });

        return interactionEvent;
    }
}
