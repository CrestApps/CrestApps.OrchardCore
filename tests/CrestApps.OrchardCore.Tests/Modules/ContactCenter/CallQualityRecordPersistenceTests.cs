using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A soft phone reports quality for the leg it holds, which the call session knows only among its legs; nothing could
/// find a session from one of those before the leg index. These run the real indexes and migrations against SQLite, so
/// a leg that is not indexed, or a record that cannot be found again, fails here rather than as a report with no calls.
/// </summary>
public sealed class CallQualityRecordPersistenceTests
{
    private static readonly DateTime _now = new(2026, 9, 23, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Observe_BrowserSummaryForAnAgentLeg_IsStoredAgainstTheInteractionAndAgent()
    {
        // Arrange
        var databasePath = DatabasePath("quality-agent-leg");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedCallSessionAsync(store);

            // Act
            await ObserveAsync(store, new CallQualityObservation
            {
                Source = CallQualitySource.Browser,
                UserId = "user-1",
                ProviderCallControlId = "agent-leg",
                Rating = CallQualityRating.Poor,
                ObservedUtc = _now,
                Browser = new CallQualityReport
                {
                    Final = true,
                    AvgMos = 3.1,
                    MaxLossPercent = 7.5,
                    JitterMs = 12,
                    RoundTripTimeMs = 80,
                    DurationMs = 95_000,
                    LocalCandidateType = "relay",
                },
            });

            // Assert
            await using var session = store.CreateSession();
            var recordStore = new CallQualityRecordStore(session);
            var record = await recordStore.FindByRecordKeyAsync(
                CallQualityRecord.BuildRecordKey(CallQualitySource.Browser, "agent-leg"),
                TestContext.Current.CancellationToken);

            Assert.NotNull(record);
            Assert.Equal("interaction-1", record.InteractionId);
            Assert.Equal("session-1", record.CallSessionId);
            Assert.Equal("queue-1", record.QueueId);
            Assert.Equal("agent-1", record.AgentId);
            Assert.Equal(CallPartyRole.Agent, record.LegRole);
            Assert.Equal(CallQualityRating.Poor, record.Rating);
            Assert.Equal(3.1, record.Mos);
            Assert.Equal(7.5, record.LossPercent);
            Assert.Equal(95, record.DurationSeconds);
            Assert.Equal("relay", record.Browser.LocalCandidateType);

            var inPeriod = await recordStore.GetObservedBetweenAsync(_now.AddMinutes(-1), _now.AddMinutes(1), TestContext.Current.CancellationToken);
            Assert.Single(inPeriod);

            var recent = await recordStore.GetRecentForAgentAsync("agent-1", _now.AddHours(-1), 5, TestContext.Current.CancellationToken);
            Assert.Single(recent);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task Observe_ProviderStatsForTheSessionsOwnCall_IsTheCustomersSide()
    {
        // Arrange
        var databasePath = DatabasePath("quality-customer-leg");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedCallSessionAsync(store);

            // Act
            await ObserveAsync(store, new CallQualityObservation
            {
                Source = CallQualitySource.Provider,
                ProviderName = "Telnyx",
                ProviderCallControlId = "customer-leg",
                Rating = CallQualityRating.Good,
                ObservedUtc = _now,
                Provider = new ProviderCallQualityStats { InboundMos = 4.4, InboundPacketCount = 990, InboundSkipPacketCount = 10 },
            });

            // Assert
            await using var session = store.CreateSession();
            var record = await new CallQualityRecordStore(session).FindByRecordKeyAsync(
                CallQualityRecord.BuildRecordKey(CallQualitySource.Provider, "customer-leg"),
                TestContext.Current.CancellationToken);

            Assert.NotNull(record);
            Assert.Equal(CallPartyRole.Customer, record.LegRole);
            Assert.Equal("interaction-1", record.InteractionId);
            Assert.Equal(4.4, record.Mos);

            // Skipped packets are not packets lost in transit, so the provider's record carries no loss figure.
            Assert.Null(record.LossPercent);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task Observe_ProviderStatsForAnAgentLeg_KeepTheirPeakJitterVarianceOutOfTheJitterFigure()
    {
        // Arrange
        var databasePath = DatabasePath("quality-provider-agent-leg");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedCallSessionAsync(store);

            // Act: the live agent leg whose peak jitter variance of 852.04 was shown as 852 ms of jitter, and whose
            // 75 skipped of 1,557 packets were read as 4.6% loss and rated it degraded.
            await ObserveAsync(store, new CallQualityObservation
            {
                Source = CallQualitySource.Provider,
                ProviderName = "Telnyx",
                ProviderCallControlId = "agent-leg",
                Rating = CallQualityRating.Degraded,
                ObservedUtc = _now,
                Provider = new ProviderCallQualityStats
                {
                    InboundMos = 4.5,
                    InboundJitterMaxVarianceMs = 852.04,
                    InboundJitterPacketCount = 0,
                    InboundPacketCount = 1557,
                    InboundSkipPacketCount = 75,
                    OutboundPacketCount = 1585,
                    OutboundSkipPacketCount = 0,
                },
            });

            // Assert
            await using var session = store.CreateSession();
            var record = await new CallQualityRecordStore(session).FindByRecordKeyAsync(
                CallQualityRecord.BuildRecordKey(CallQualitySource.Provider, "agent-leg"),
                TestContext.Current.CancellationToken);

            Assert.NotNull(record);
            Assert.Equal(CallPartyRole.Agent, record.LegRole);
            Assert.Equal(CallQualityRating.Good, record.Rating);
            Assert.Equal(4.5, record.Mos);
            Assert.Null(record.LossPercent);
            Assert.Null(record.JitterMs);
            Assert.Equal(852.04, record.Provider.InboundJitterMaxVarianceMs);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    // The live agent legs of four one-way calls: the provider sent the caller's audio to the agent (572 packets) and
    // received nothing back. With no packets there is no opinion score, and the leg was rated Good.
    [Fact]
    public async Task Observe_ProviderStatsForAnAgentLegThatReceivedNothing_IsPoorForNoAudioSent()
    {
        // Arrange
        var databasePath = DatabasePath("quality-provider-agent-one-way");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedCallSessionAsync(store);

            // Act
            await ObserveAsync(store, new CallQualityObservation
            {
                Source = CallQualitySource.Provider,
                ProviderName = "Telnyx",
                ProviderCallControlId = "agent-leg",
                Rating = CallQualityRating.Good,
                ObservedUtc = _now,
                Provider = new ProviderCallQualityStats
                {
                    InboundMos = 4.5,
                    InboundPacketCount = 0,
                    InboundSkipPacketCount = 596,
                    OutboundPacketCount = 572,
                    OutboundSkipPacketCount = 0,
                },
            });

            // Assert
            await using var session = store.CreateSession();
            var record = await new CallQualityRecordStore(session).FindByRecordKeyAsync(
                CallQualityRecord.BuildRecordKey(CallQualitySource.Provider, "agent-leg"),
                TestContext.Current.CancellationToken);

            Assert.NotNull(record);
            Assert.Equal(CallPartyRole.Agent, record.LegRole);
            Assert.Equal(CallQualityRating.Poor, record.Rating);
            Assert.Null(record.Mos);
            Assert.Equal(CallQualityCause.NoAudioSent, CallQualityCauseClassifier.Classify(record));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    // A customer who sends nothing is the customer's line, not the agent's microphone; it is not rated on this.
    [Fact]
    public async Task Observe_ProviderStatsForTheCustomersLegThatReceivedNothing_IsNotRatedAsNoAudioSent()
    {
        // Arrange
        var databasePath = DatabasePath("quality-provider-customer-silent");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedCallSessionAsync(store);

            // Act
            await ObserveAsync(store, new CallQualityObservation
            {
                Source = CallQualitySource.Provider,
                ProviderName = "Telnyx",
                ProviderCallControlId = "customer-leg",
                Rating = CallQualityRating.Good,
                ObservedUtc = _now,
                Provider = new ProviderCallQualityStats { InboundMos = 4.5, InboundPacketCount = 0, InboundSkipPacketCount = 300, OutboundPacketCount = 280 },
            });

            // Assert
            await using var session = store.CreateSession();
            var record = await new CallQualityRecordStore(session).FindByRecordKeyAsync(
                CallQualityRecord.BuildRecordKey(CallQualitySource.Provider, "customer-leg"),
                TestContext.Current.CancellationToken);

            Assert.NotNull(record);
            Assert.Equal(CallPartyRole.Customer, record.LegRole);
            Assert.Equal(CallQualityRating.Good, record.Rating);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task Observe_ALegNoSessionKnows_IsStillRecordedAgainstTheAgent()
    {
        // Arrange
        var databasePath = DatabasePath("quality-extension-call");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            // Act: an extension call between two agents, which the contact center did not route.
            await ObserveAsync(store, new CallQualityObservation
            {
                Source = CallQualitySource.Browser,
                UserId = "user-1",
                ProviderCallControlId = "extension-leg",
                Rating = CallQualityRating.Degraded,
                ObservedUtc = _now,
                Browser = new CallQualityReport { Final = true, Mos = 3.9 },
            });

            // Assert
            await using var session = store.CreateSession();
            var record = await new CallQualityRecordStore(session).FindByRecordKeyAsync(
                CallQualityRecord.BuildRecordKey(CallQualitySource.Browser, "extension-leg"),
                TestContext.Current.CancellationToken);

            Assert.NotNull(record);
            Assert.Null(record.InteractionId);
            Assert.Equal("agent-1", record.AgentId);
            Assert.Equal(CallPartyRole.Agent, record.LegRole);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task Observe_TheSameMeasurementTwice_IsRecordedOnce()
    {
        // Arrange
        var databasePath = DatabasePath("quality-redelivery");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var observation = new CallQualityObservation
            {
                Source = CallQualitySource.Provider,
                ProviderCallControlId = "customer-leg",
                Rating = CallQualityRating.Good,
                ObservedUtc = _now,
                Provider = new ProviderCallQualityStats { InboundMos = 4.4 },
            };

            // Act: a redelivered hangup webhook.
            await ObserveAsync(store, observation);
            await ObserveAsync(store, observation);

            // Assert
            await using var session = store.CreateSession();
            var records = await new CallQualityRecordStore(session).GetObservedBetweenAsync(_now.AddMinutes(-1), _now.AddMinutes(1), TestContext.Current.CancellationToken);

            Assert.Single(records);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task ObserveAsync(IStore store, CallQualityObservation observation)
    {
        await using var session = store.CreateSession();

        var agentProfileStore = new Mock<IAgentProfileStore>();
        agentProfileStore
            .Setup(profiles => profiles.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var observer = new ContactCenterCallQualityObserver(
            new CallQualityRecordStore(session),
            new CallSessionStore(session),
            agentProfileStore.Object,
            Mock.Of<ICallQualityAlertService>(),
            clock.Object,
            NullLogger<ContactCenterCallQualityObserver>.Instance);

        await observer.ObserveAsync(observation, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"contact-center-{prefix}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new CallSessionIndexProvider(new ProviderIdentityResolver([])),
            new CallSessionLegIndexProvider(),
            new CallQualityRecordIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await new CallSessionIndexMigrations(store, new ProviderIdentityResolver([])) { SchemaBuilder = schemaBuilder }.CreateAsync();
        await new CallSessionLegIndexMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
        await new CallQualityRecordIndexMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SeedCallSessionAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var callSession = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderCallId = "customer-leg",
            QueueId = "queue-1",
            AgentId = "agent-1",
            CreatedUtc = _now.AddMinutes(-2),
            Legs =
            [
                new CallLeg { ProviderLegId = "customer-leg", Role = CallPartyRole.Customer, StartedUtc = _now.AddMinutes(-2) },
                new CallLeg { ProviderLegId = "agent-leg", Role = CallPartyRole.Agent, AgentId = "agent-1", StartedUtc = _now.AddMinutes(-1) },
            ],
        };

        await new CallSessionStore(session).CreateAsync(callSession, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
