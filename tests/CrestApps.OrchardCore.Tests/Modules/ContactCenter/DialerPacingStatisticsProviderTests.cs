using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Predictive dialing sizes its over-dial from what a profile's recent calls measured. These run the statement production
/// runs over a real store holding calls of every outcome, seeded as the platform records them, and pin what is counted:
/// calls still ringing stay out of the answer rate (counting them as unanswered would place more calls), the answered and
/// abandoned counts are the ones the abandonment cap is held to, and other profiles' and older calls are left out.
/// </summary>
public sealed class DialerPacingStatisticsProviderTests
{
    private const string ProfileId = "profile-1";

    private static readonly DateTime _t0 = new(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _now = _t0.AddSeconds(600);
    private static readonly TimeSpan _window = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task GetStatisticsAsync_CountsTheProfilesCallsInTheWindow()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateSeededStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();

            // Act
            var statistics = await CreateProvider(session).GetStatisticsAsync(ProfileId, _window, cancellationToken);

            // Assert
            Assert.NotNull(statistics);
            Assert.Equal(_now - _window, statistics.FromUtc);
            Assert.Equal(_now, statistics.ToUtc);
            Assert.Equal(6, statistics.Attempts);
            Assert.Equal(3, statistics.LiveAnswers);
            Assert.Equal(1, statistics.AbandonedCalls);
            Assert.Equal(100d / 3, statistics.AbandonmentRatePercent.Value, 1e-9);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetStatisticsAsync_LeavesCallsStillRingingOutOfTheAnswerRate()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateSeededStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();

            // Act
            var statistics = await CreateProvider(session).GetStatisticsAsync(ProfileId, _window, cancellationToken);

            // Assert: of the six calls, the one still ringing has no outcome; four of the other five were answered, the
            // one an agent was connected to without a recorded live answer included.
            Assert.Equal(5, statistics.SettledAttempts);
            Assert.Equal(4, statistics.SettledLiveAnswers);
            Assert.Equal(0.8, statistics.AnswerRate.Value, 1e-9);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetStatisticsAsync_MeasuresRingConnectTalkAndWrapUpTimes()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateSeededStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();

            // Act
            var statistics = await CreateProvider(session).GetStatisticsAsync(ProfileId, _window, cancellationToken);

            // Assert: answered after 10 s, 6 s and 14 s.
            Assert.Equal(3, statistics.RingToAnswerSamples);
            Assert.Equal(TimeSpan.FromSeconds(10), statistics.MedianRingToAnswer);
            Assert.Equal(TimeSpan.FromSeconds(14), statistics.P75RingToAnswer);

            // Agents connected 1.5 s and 0.5 s after the answer.
            Assert.Equal(2, statistics.ConnectLatencySamples);
            Assert.Equal(TimeSpan.FromSeconds(0.5), statistics.MedianConnectLatency);
            Assert.Equal(TimeSpan.FromSeconds(1.5), statistics.P95ConnectLatency);

            // Talked 60 s, 120 s and 60 s, wrapped up 30 s and 10 s.
            Assert.Equal(3, statistics.TalkTimeSamples);
            Assert.Equal(TimeSpan.FromSeconds(80), statistics.AverageTalkTime);
            Assert.Equal(2, statistics.WrapUpTimeSamples);
            Assert.Equal(TimeSpan.FromSeconds(20), statistics.AverageWrapUpTime);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetStatisticsAsync_SamplesOnlyTheMostRecentCalls()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateSeededStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();

            // Act
            var statistics = await CreateProvider(session, new ContactCenterPredictiveDialingOptions { MaxTimingSamples = 2 })
                .GetStatisticsAsync(ProfileId, _window, cancellationToken);

            // Assert: the two newest calls are the one still ringing and the abandoned one; every call is still counted.
            Assert.Equal(6, statistics.Attempts);
            Assert.Equal(1, statistics.SettledAttempts);
            Assert.Equal(1, statistics.SettledLiveAnswers);
            Assert.Equal(TimeSpan.FromSeconds(14), statistics.MedianRingToAnswer);
            Assert.Equal(0, statistics.ConnectLatencySamples);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetStatisticsAsync_ForAProfileWithNoCalls_ReportsNothingMeasured()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateSeededStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();

            // Act
            var statistics = await CreateProvider(session).GetStatisticsAsync("profile-without-calls", _window, cancellationToken);

            // Assert
            Assert.NotNull(statistics);
            Assert.Equal(0, statistics.Attempts);
            Assert.Null(statistics.AnswerRate);
            Assert.Null(statistics.AbandonmentRatePercent);
            Assert.Null(statistics.MedianRingToAnswer);
            Assert.Null(statistics.AverageTalkTime);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetStatisticsAsync_WhenTheAbandonmentCountsCannotBeRead_ReportsNothing()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateSeededStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();
            var provider = new InteractionEventDialerPacingStatisticsProvider(
                session,
                [],
                new DialerPacingStatisticsCache(),
                Options.Create(new ContactCenterPredictiveDialingOptions()),
                Clock(_now));

            // Act
            var statistics = await provider.GetStatisticsAsync(ProfileId, _window, cancellationToken);

            // Assert: pacing then does not over-dial.
            Assert.Null(statistics);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetStatisticsAsync_ReusesAMeasurementUntilItExpires()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateSeededStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();
            var cache = new DialerPacingStatisticsCache();
            var options = new ContactCenterPredictiveDialingOptions { StatisticsCacheDuration = TimeSpan.FromSeconds(5) };

            // Act
            var first = await CreateProvider(session, options, cache, _now).GetStatisticsAsync(ProfileId, _window, cancellationToken);
            var reused = await CreateProvider(session, options, cache, _now.AddSeconds(4)).GetStatisticsAsync(ProfileId, _window, cancellationToken);
            var remeasured = await CreateProvider(session, options, cache, _now.AddSeconds(6)).GetStatisticsAsync(ProfileId, _window, cancellationToken);

            // Assert
            Assert.Same(first, reused);
            Assert.NotSame(first, remeasured);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Theory]
    [InlineData(null, 15)]
    [InlineData("", 15)]
    [InlineData(ProfileId, 0)]
    public async Task GetStatisticsAsync_WithoutAProfileOrAWindow_ReportsNothing(string profileId, int windowMinutes)
    {
        // Arrange
        var provider = new InteractionEventDialerPacingStatisticsProvider(
            Mock.Of<ISession>(),
            [],
            new DialerPacingStatisticsCache(),
            Options.Create(new ContactCenterPredictiveDialingOptions()),
            Clock(_now));

        // Act
        var statistics = await provider.GetStatisticsAsync(profileId, TimeSpan.FromMinutes(windowMinutes), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(statistics);
    }

    private static InteractionEventDialerPacingStatisticsProvider CreateProvider(
        ISession session,
        ContactCenterPredictiveDialingOptions options = null,
        DialerPacingStatisticsCache cache = null,
        DateTime? now = null)
    {
        var clock = Clock(now ?? _now);

        return new InteractionEventDialerPacingStatisticsProvider(
            session,
            [new InteractionEventDialerAbandonmentStatisticsProvider(session, clock)],
            cache ?? new DialerPacingStatisticsCache(),
            Options.Create(options ?? new ContactCenterPredictiveDialingOptions()),
            clock);
    }

    private static IClock Clock(DateTime now)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(now);

        return clock.Object;
    }

    private static async Task<(IStore Store, string DatabasePath)> CreateSeededStoreAsync(CancellationToken cancellationToken)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-dialer-pacing-{Guid.NewGuid():N}.db");
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

        // Answered after 10 s, an agent 1.5 s later; talked a minute and wrapped up for 30 s.
        await seed.CallAsync("call-1", ProfileId, placed: 0, liveAnswered: 10, agentJoined: 11.5, ended: 71.5, wrapUpCompleted: 101.5);

        // Answered after 6 s, an agent half a second later; talked two minutes and wrapped up for 10 s.
        await seed.CallAsync("call-2", ProfileId, placed: 20, liveAnswered: 26, agentJoined: 26.5, ended: 146.5, wrapUpCompleted: 156.5);

        // Connected to an agent without a recorded live answer, as calls placed before live answers were recorded are;
        // talked a minute.
        await seed.CallAsync("call-legacy", ProfileId, placed: 30, agentJoined: 40, ended: 100);

        // Rang out.
        await seed.CallAsync("call-3", ProfileId, placed: 40, ended: 70);

        // Answered after 14 s and abandoned: no agent could be connected.
        await seed.CallAsync("call-4", ProfileId, placed: 60, liveAnswered: 74, abandoned: 77, ended: 90);

        // Still ringing.
        await seed.CallAsync("call-5", ProfileId, placed: 590);

        // Placed before the window opened.
        await seed.CallAsync("call-old", ProfileId, placed: -1000, liveAnswered: -990, abandoned: -985, ended: -980);

        // Another profile's call.
        await seed.CallAsync("call-other", "profile-2", placed: 100, liveAnswered: 105, abandoned: 108, ended: 110);

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

        public async Task CallAsync(
            string id,
            string profileId,
            double placed,
            double? liveAnswered = null,
            double? agentJoined = null,
            double? abandoned = null,
            double? ended = null,
            double? wrapUpCompleted = null)
        {
            var interaction = new Interaction
            {
                ItemId = id,
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Outbound,
                CreatedUtc = _t0.AddSeconds(placed),
                AnsweredUtc = At(liveAnswered),
                EndedUtc = At(ended),
                WrapUpStartedUtc = wrapUpCompleted.HasValue ? At(ended) : null,
                WrapUpCompletedUtc = At(wrapUpCompleted),
            }.RestorePersistedStatus(ended.HasValue ? InteractionStatus.Ended : InteractionStatus.Created);

            await _session.SaveAsync(interaction, collection: ContactCenterStorage.CollectionName);

            await EventAsync(id, ContactCenterConstants.Events.DialerAttemptStarted, nameof(DialerProfile), profileId, placed);

            if (liveAnswered.HasValue)
            {
                await EventAsync(id, ContactCenterConstants.Events.DialerLiveAnswered, nameof(DialerProfile), profileId, liveAnswered.Value);
            }

            if (agentJoined.HasValue)
            {
                await EventAsync(id, ContactCenterConstants.Events.AgentLegAnswered, nameof(Interaction), id, agentJoined.Value);
            }

            if (abandoned.HasValue)
            {
                await EventAsync(id, ContactCenterConstants.Events.DialerCallAbandoned, nameof(DialerProfile), profileId, abandoned.Value);
            }

            if (ended.HasValue)
            {
                await EventAsync(id, ContactCenterConstants.Events.CallEnded, nameof(Interaction), id, ended.Value);
            }
        }

        private static DateTime? At(double? seconds)
            => seconds.HasValue ? _t0.AddSeconds(seconds.Value) : null;

        private Task EventAsync(string interactionId, string eventType, string aggregateType, string aggregateId, double at)
        {
            var interactionEvent = new InteractionEvent
            {
                ItemId = $"event-{++_sequence}",
                InteractionId = interactionId,
                EventType = eventType,
                AggregateType = aggregateType,
                AggregateId = aggregateId,
                OccurredUtc = _t0.AddSeconds(at),
                RecordedUtc = _t0.AddSeconds(at),
                SchemaVersion = ContactCenterStorage.CurrentEventSchemaVersion,
                IdempotencyKey = $"{eventType}:{interactionId}:{_sequence}",
            };

            return _session.SaveAsync(interactionEvent, collection: ContactCenterStorage.CollectionName);
        }
    }
}
