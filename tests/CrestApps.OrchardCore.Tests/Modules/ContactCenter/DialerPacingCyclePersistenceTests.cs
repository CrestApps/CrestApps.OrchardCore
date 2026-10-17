using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Runs the pacing cycle record against SQLite with the real index and migration: a record is written on a session of
/// its own, so a cycle whose own session is rolled back still leaves one; repeated cycles that found no agent are folded
/// into one record; and the retention policy ages records out.
/// </summary>
public sealed class DialerPacingCyclePersistenceTests
{
    private static readonly string _queue = ContactCenterConstants.CampaignQueue.CreateId("campaign-1");
    private static readonly DateTime _start = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ACycleRolledBackOnItsOwnSession_IsStillRecorded()
    {
        // Arrange
        var databasePath = DatabasePath("pacing-rolled-back");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var clock = new MutableClock(_start);
            var recorder = CreateRecorder(store, clock);

            // Act: the cycle staged work on its own session and then rolled it back, as a failed cycle does.
            await using (var cycleSession = store.CreateSession())
            {
                await new DialerPacingCycleStore(cycleSession).CreateAsync(
                    new DialerPacingCycle { QueueId = _queue, Outcome = DialerPacingCycleOutcome.Placed, CompletedUtc = _start },
                    TestContext.Current.CancellationToken);

                await recorder.RecordAsync(new DialerPacingCycle
                {
                    QueueId = _queue,
                    DialerProfileId = "dp1",
                    Outcome = DialerPacingCycleOutcome.Failed,
                    Detail = "InvalidOperationException: The provider refused the call.",
                    StartedUtc = _start,
                });

                await cycleSession.CancelAsync();
            }

            // Assert
            await using var session = store.CreateSession();
            var records = await new DialerPacingCycleStore(session).GetCompletedBetweenAsync(_start.AddHours(-1), _start.AddHours(1), TestContext.Current.CancellationToken);
            var record = Assert.Single(records);
            Assert.Equal(DialerPacingCycleOutcome.Failed, record.Outcome);
            Assert.Equal("campaign-1", record.CampaignId);
            Assert.Equal(_start, record.CompletedUtc);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RepeatedCyclesThatFoundNoAgent_AreFoldedIntoOneRecord_UntilTheOutcomeChanges()
    {
        // Arrange
        var databasePath = DatabasePath("pacing-folded");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var clock = new MutableClock(_start);
            var recorder = CreateRecorder(store, clock);

            // Act: three cycles a minute apart find no agent, then one places a call.
            for (var minute = 0; minute < 3; minute++)
            {
                clock.UtcNow = _start.AddMinutes(minute);
                await recorder.RecordAsync(Cycle(DialerPacingCycleOutcome.NoAgents, clock.UtcNow, routingNoAgent: 1));
            }

            clock.UtcNow = _start.AddMinutes(3);
            await recorder.RecordAsync(Cycle(DialerPacingCycleOutcome.Placed, clock.UtcNow, placed: 1));

            // Assert
            await using var session = store.CreateSession();
            var cycleStore = new DialerPacingCycleStore(session);
            var all = await cycleStore.GetCompletedBetweenAsync(_start.AddHours(-1), _start.AddHours(1), TestContext.Current.CancellationToken);

            Assert.Equal(2, all.Count);

            var noAgents = await cycleStore.FindLatestByQueueAsync(_queue, [DialerPacingCycleOutcome.NoAgents], TestContext.Current.CancellationToken);
            Assert.Equal(3, noAgents.CycleCount);
            Assert.Equal(3, noAgents.RoutingNoAgent);
            Assert.Equal(_start, noAgents.FirstStartedUtc);
            Assert.Equal(_start.AddMinutes(2), noAgents.StartedUtc);
            Assert.Equal(_start.AddMinutes(2), noAgents.CompletedUtc);

            var latest = await cycleStore.FindLatestByQueueAsync(_queue, outcomes: null, TestContext.Current.CancellationToken);
            Assert.Equal(DialerPacingCycleOutcome.Placed, latest.Outcome);
            Assert.Equal(1, latest.CallsPlaced);

            var byProfile = await cycleStore.FindLatestByProfileAsync("dp1", [DialerPacingCycleOutcome.Placed], TestContext.Current.CancellationToken);
            Assert.Equal(latest.ItemId, byProfile.ItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task CyclesThatFoundNoAgent_StartANewRecordEveryHour()
    {
        // Arrange
        var databasePath = DatabasePath("pacing-fold-window");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var clock = new MutableClock(_start);
            var recorder = CreateRecorder(store, clock);

            // Act
            await recorder.RecordAsync(Cycle(DialerPacingCycleOutcome.NoAgents, _start));
            clock.UtcNow = _start.Add(DialerPacingCycleRecorder.FoldWindow).AddMinutes(1);
            await recorder.RecordAsync(Cycle(DialerPacingCycleOutcome.NoAgents, clock.UtcNow));

            // Assert
            await using var session = store.CreateSession();
            var all = await new DialerPacingCycleStore(session).GetCompletedBetweenAsync(_start.AddHours(-1), _start.AddHours(3), TestContext.Current.CancellationToken);
            Assert.Equal(2, all.Count);
            Assert.All(all, record => Assert.Equal(1, record.CycleCount));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task TheRetentionPolicy_PurgesRecordsOlderThanItsWindow()
    {
        // Arrange
        var databasePath = DatabasePath("pacing-retention");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var clock = new MutableClock(_start.AddDays(-20));
            var recorder = CreateRecorder(store, clock);

            await recorder.RecordAsync(Cycle(DialerPacingCycleOutcome.Placed, clock.UtcNow, placed: 1));
            clock.UtcNow = _start;
            await recorder.RecordAsync(Cycle(DialerPacingCycleOutcome.Placed, clock.UtcNow, placed: 1));

            var options = new ContactCenterRetentionOptions();

            // Act
            int purged;

            await using (var session = store.CreateSession())
            {
                var policy = new DialerPacingCycleRetentionPolicy(session, new DialerPacingCycleStore(session));

                Assert.True(policy.TryGetCutoff(_start, options, out var cutoffUtc));
                purged = await policy.PurgeBatchAsync(cutoffUtc, 100, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert: the default window keeps the last fourteen days.
            Assert.Equal(1, purged);

            await using var readSession = store.CreateSession();
            var remaining = await new DialerPacingCycleStore(readSession).GetCompletedBetweenAsync(_start.AddDays(-30), _start.AddDays(1), TestContext.Current.CancellationToken);
            Assert.Equal(_start, Assert.Single(remaining).CompletedUtc);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Theory]
    [InlineData(DialerPacingCycleOutcome.NoAgents, DialerPacingCycleOutcome.NoAgents, true)]
    [InlineData(DialerPacingCycleOutcome.NothingDue, DialerPacingCycleOutcome.NothingDue, true)]
    [InlineData(DialerPacingCycleOutcome.LockNotAcquired, DialerPacingCycleOutcome.LockNotAcquired, true)]
    [InlineData(DialerPacingCycleOutcome.Placed, DialerPacingCycleOutcome.Placed, false)]
    [InlineData(DialerPacingCycleOutcome.Failed, DialerPacingCycleOutcome.Failed, false)]
    [InlineData(DialerPacingCycleOutcome.NoAgents, DialerPacingCycleOutcome.NothingDue, false)]
    public void OnlyCyclesThatPlacedNothingAndEndedTheSameWay_AreFolded(
        DialerPacingCycleOutcome latest,
        DialerPacingCycleOutcome next,
        bool folded)
    {
        // Arrange
        var latestRecord = Cycle(latest, _start);
        latestRecord.FirstStartedUtc = _start;
        latestRecord.CompletedUtc = _start;

        var nextRecord = Cycle(next, _start.AddMinutes(1));
        nextRecord.CompletedUtc = _start.AddMinutes(1);

        // Act
        var canFold = DialerPacingCycleRecorder.CanFold(latestRecord, nextRecord);

        // Assert
        Assert.Equal(folded, canFold);
    }

    private static DialerPacingCycle Cycle(DialerPacingCycleOutcome outcome, DateTime startedUtc, int placed = 0, int routingNoAgent = 0)
        => new()
        {
            QueueId = _queue,
            DialerProfileId = "dp1",
            Outcome = outcome,
            StartedUtc = startedUtc,
            CallsPlaced = placed,
            RoutingNoAgent = routingNoAgent,
        };

    private static DialerPacingCycleRecorder CreateRecorder(IStore store, IClock clock)
        => new(new SessionScopeExecutor(store), clock, NullLogger<DialerPacingCycleRecorder>.Instance);

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"contact-center-{prefix}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new DialerPacingCycleIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await new DialerPacingCycleIndexMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    /// <summary>
    /// Runs each operation on a session of its own, as a child shell scope does, and has no scope to defer work to.
    /// </summary>
    private sealed class SessionScopeExecutor : IContactCenterScopeExecutor
    {
        private readonly IStore _store;

        public SessionScopeExecutor(IStore store)
        {
            _store = store;
        }

        public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => throw new NotSupportedException();

        public async Task ExecuteAsync(Func<IServiceProvider, Task> operation)
        {
            await using var session = _store.CreateSession();

            var services = new ServiceCollection();
            services.AddSingleton(session);
            services.AddSingleton<IDialerPacingCycleStore>(new DialerPacingCycleStore(session));

            await using var provider = services.BuildServiceProvider();

            await operation(provider);
        }

        public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => false;

        public bool ScheduleAfterCommit(Func<Task> operation)
            => false;
    }

    private sealed class MutableClock : IClock
    {
        public MutableClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }

        public ITimeZone GetTimeZone(string timeZoneId) => throw new NotSupportedException();

        public ITimeZone GetSystemTimeZone() => throw new NotSupportedException();

        public ITimeZone[] GetTimeZones() => [];

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;
    }
}
