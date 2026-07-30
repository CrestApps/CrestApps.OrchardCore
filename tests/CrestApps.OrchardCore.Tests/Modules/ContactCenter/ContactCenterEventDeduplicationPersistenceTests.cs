using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterEventDeduplicationPersistenceTests
{
    private static readonly DateTime _now = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandlerScope_WhenEffectThrowsBeforeCommit_RollsBackMarkerAndEffect()
    {
        // Arrange
        var databasePath = DatabasePath("rollback");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var failedSession = store.CreateSession())
            {
                var deduplication = new ContactCenterEventDeduplicationService(failedSession, CreateClock());
                var metrics = new ContactCenterMetricsService(
                    new ContactCenterMetricStore(failedSession),
                    new ContactCenterMetricDeltaStore(failedSession),
                    CreateClock());

                Assert.True(await deduplication.TryBeginAsync(
                    "ContactCenter/MetricsProjection/v1",
                    "event-1",
                    TestContext.Current.CancellationToken));
                await metrics.RecordAsync(
                    "OfferAccepted",
                    _now,
                    TestContext.Current.CancellationToken);

                // Simulate the isolated handler scope unwinding after its effect throws. No SaveChangesAsync
                // occurs, so both staged documents must be discarded when the session is disposed.
            }

            await using (var verificationSession = store.CreateSession())
            {
                var marker = await verificationSession
                    .Query<ContactCenterProcessedEvent, ContactCenterProcessedEventIndex>(
                        index =>
                            index.HandlerId == "ContactCenter/MetricsProjection/v1" &&
                            index.EventId == "event-1",
                        collection: ContactCenterConstants.CollectionName)
                    .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
                var contributions = await new ContactCenterMetricDeltaStore(verificationSession)
                    .ListByDateRangeAsync(_now.Date, _now.Date, TestContext.Current.CancellationToken);

                Assert.Null(marker);
                Assert.Empty(contributions);
            }

            await using (var retrySession = store.CreateSession())
            {
                var deduplication = new ContactCenterEventDeduplicationService(retrySession, CreateClock());
                var metrics = new ContactCenterMetricsService(
                    new ContactCenterMetricStore(retrySession),
                    new ContactCenterMetricDeltaStore(retrySession),
                    CreateClock());

                Assert.True(await deduplication.TryBeginAsync(
                    "ContactCenter/MetricsProjection/v1",
                    "event-1",
                    TestContext.Current.CancellationToken));
                await metrics.RecordAsync(
                    "OfferAccepted",
                    _now,
                    TestContext.Current.CancellationToken);
                await retrySession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var committedSession = store.CreateSession();
            var committed = await new ContactCenterMetricDeltaStore(committedSession)
                .ListByDateRangeAsync(_now.Date, _now.Date, TestContext.Current.CancellationToken);

            var contribution = Assert.Single(committed);

            Assert.Equal("2026-07-15", contribution.DateKey);
            Assert.Equal("OfferAccepted", contribution.EventType);
            Assert.Equal(1, contribution.Count);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task TryBeginAsync_WhenTwoSessionsReserveSameEvent_UniqueIndexAllowsOneCommit()
    {
        // Arrange
        var databasePath = DatabasePath("concurrent");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var firstSession = store.CreateSession();
            await using var secondSession = store.CreateSession();
            var first = new ContactCenterEventDeduplicationService(firstSession, CreateClock());
            var second = new ContactCenterEventDeduplicationService(secondSession, CreateClock());

            Assert.True(await first.TryBeginAsync("handler/v1", "event-1", TestContext.Current.CancellationToken));
            Assert.True(await second.TryBeginAsync("handler/v1", "event-1", TestContext.Current.CancellationToken));

            // Act
            await firstSession.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            await Assert.ThrowsAnyAsync<DbException>(() =>
                secondSession.SaveChangesAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static string DatabasePath(string suffix)
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"contact-center-processed-event-{suffix}-{Guid.NewGuid():N}.db");
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new ContactCenterProcessedEventIndexProvider(),
            new ContactCenterEventMetricIndexProvider(),
            new ContactCenterEventMetricDeltaIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            ContactCenterConstants.CollectionName,
            TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        var processedEventMigration = new ContactCenterProcessedEventIndexMigrations(store, new StubClock())
        {
            SchemaBuilder = schemaBuilder,
        };
        var metricMigration = new ContactCenterEventMetricIndexMigrations(store)
        {
            SchemaBuilder = schemaBuilder,
        };
        var deltaMigration = new ContactCenterEventMetricDeltaIndexMigrations
        {
            SchemaBuilder = schemaBuilder,
        };

        await processedEventMigration.CreateAsync();

        // The create step stops at its shipped version, exactly as it does for a real tenant, so the retention
        // column arrives through the update step here too.
        await processedEventMigration.UpdateFrom1Async();
        await metricMigration.CreateAsync();
        await deltaMigration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
