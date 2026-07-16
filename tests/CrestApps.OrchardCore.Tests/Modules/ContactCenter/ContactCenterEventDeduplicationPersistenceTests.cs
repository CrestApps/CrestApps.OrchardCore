using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
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
                var metric = await new ContactCenterMetricStore(verificationSession).FindAsync(
                    "2026-07-15",
                    "OfferAccepted",
                    TestContext.Current.CancellationToken);

                Assert.Null(marker);
                Assert.Null(metric);
            }

            await using (var retrySession = store.CreateSession())
            {
                var deduplication = new ContactCenterEventDeduplicationService(retrySession, CreateClock());
                var metrics = new ContactCenterMetricsService(
                    new ContactCenterMetricStore(retrySession),
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
            var committedMetric = await new ContactCenterMetricStore(committedSession).FindAsync(
                "2026-07-15",
                "OfferAccepted",
                TestContext.Current.CancellationToken);

            Assert.NotNull(committedMetric);
            Assert.Equal(1, committedMetric.Count);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
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
            store.Dispose();
            File.Delete(databasePath);
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
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            ContactCenterConstants.CollectionName,
            TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        var processedEventMigration = new ContactCenterProcessedEventIndexMigrations(store)
        {
            SchemaBuilder = schemaBuilder,
        };
        var metricMigration = new ContactCenterEventMetricIndexMigrations(store)
        {
            SchemaBuilder = schemaBuilder,
        };

        await processedEventMigration.CreateAsync();
        await metricMigration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
