using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterMetricsProjectionMaintenanceTests
{
    private static readonly DateTime _dayA = new(2026, 3, 1, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime _dayB = new(2026, 3, 2, 14, 15, 0, DateTimeKind.Utc);
    private static readonly DateTime _now = new(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc);

    private const string CreatedEvent = "InteractionCreated";
    private const string ClosedEvent = "InteractionClosed";

    [Fact]
    public async Task RebuildAsync_RecomputesCorrectsMetricsAndAdvancesCheckpoint()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-projection-rebuild-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveEventAsync(seedSession, "e1", CreatedEvent, _dayA);
                await SaveEventAsync(seedSession, "e2", CreatedEvent, _dayA.AddMinutes(5));
                await SaveEventAsync(seedSession, "e3", CreatedEvent, _dayA.AddMinutes(10));
                await SaveEventAsync(seedSession, "e4", ClosedEvent, _dayA.AddMinutes(20));
                await SaveEventAsync(seedSession, "e5", CreatedEvent, _dayB);
                await SaveEventAsync(seedSession, "e6", CreatedEvent, _dayB.AddMinutes(30));

                // A stale, incorrect metric and an orphaned metric that the rebuild must reconcile.
                await SaveMetricAsync(seedSession, "m-wrong", "2026-03-01", CreatedEvent, _dayA, 99);
                await SaveMetricAsync(seedSession, "m-orphan", "2026-02-15", "GhostEvent", new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), 5);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            int changes;

            await using (var actSession = store.CreateSession())
            {
                var service = CreateService(actSession);

                // Act
                changes = await service.RebuildAsync(TestContext.Current.CancellationToken);

                await actSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            await using var assertSession = store.CreateSession();
            var metricStore = new ContactCenterMetricStore(assertSession);

            var createdDayA = await metricStore.FindAsync("2026-03-01", CreatedEvent, TestContext.Current.CancellationToken);
            var closedDayA = await metricStore.FindAsync("2026-03-01", ClosedEvent, TestContext.Current.CancellationToken);
            var createdDayB = await metricStore.FindAsync("2026-03-02", CreatedEvent, TestContext.Current.CancellationToken);
            var orphan = await metricStore.FindAsync("2026-02-15", "GhostEvent", TestContext.Current.CancellationToken);

            Assert.NotNull(createdDayA);
            Assert.Equal(3, createdDayA.Count);
            Assert.NotNull(closedDayA);
            Assert.Equal(1, closedDayA.Count);
            Assert.NotNull(createdDayB);
            Assert.Equal(2, createdDayB.Count);
            Assert.Null(orphan);

            Assert.True(changes >= 3);

            var checkpointStore = new ContactCenterProjectionCheckpointStore(assertSession);
            var checkpoint = await checkpointStore.FindByHandlerAsync(ContactCenterConstants.MetricsProjectionHandlerId, TestContext.Current.CancellationToken);

            Assert.NotNull(checkpoint);
            Assert.Equal(ContactCenterConstants.MetricsProjectionVersion, checkpoint.Version);
            Assert.Equal(_dayB.AddMinutes(30), checkpoint.LastOccurredUtc);
            Assert.Equal(_now, checkpoint.RebuiltUtc);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task DetectDriftAsync_ReportsMismatchBetweenLogAndProjection()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-projection-drift-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveEventAsync(seedSession, "e1", CreatedEvent, _dayA);
                await SaveEventAsync(seedSession, "e2", CreatedEvent, _dayA.AddMinutes(5));

                await SaveMetricAsync(seedSession, "m-wrong", "2026-03-01", CreatedEvent, _dayA, 1);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var actSession = store.CreateSession();
            var service = CreateService(actSession);

            // Act
            var drifts = await service.DetectDriftAsync(TestContext.Current.CancellationToken);

            // Assert
            var drift = Assert.Single(drifts);
            Assert.Equal("2026-03-01", drift.DateKey);
            Assert.Equal(CreatedEvent, drift.EventType);
            Assert.Equal(2, drift.ExpectedCount);
            Assert.Equal(1, drift.ActualCount);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task DetectDriftAsync_ReturnsEmptyAfterRebuild()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-projection-clean-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveEventAsync(seedSession, "e1", CreatedEvent, _dayA);
                await SaveEventAsync(seedSession, "e2", ClosedEvent, _dayA.AddMinutes(5));
                await SaveEventAsync(seedSession, "e3", CreatedEvent, _dayB);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var rebuildSession = store.CreateSession())
            {
                var rebuildService = CreateService(rebuildSession);
                await rebuildService.RebuildAsync(TestContext.Current.CancellationToken);
                await rebuildSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var assertSession = store.CreateSession();
            var service = CreateService(assertSession);

            // Act
            var drifts = await service.DetectDriftAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(drifts);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static ContactCenterMetricsProjectionMaintenanceService CreateService(ISession session)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);

        return new ContactCenterMetricsProjectionMaintenanceService(
            new InteractionEventStore(session),
            new ContactCenterMetricStore(session),
            new ContactCenterProjectionCheckpointStore(session),
            clock.Object);
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new InteractionEventIndexProvider(),
            new ContactCenterEventMetricIndexProvider(),
            new ContactCenterProjectionCheckpointIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<InteractionEventIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("EventType", column => column.WithLength(128))
            .Column<string>("AggregateType", column => column.WithLength(128))
            .Column<string>("AggregateId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<string>("IdempotencyKey", column => column.WithLength(128))
            .Column<string>("IdempotencyClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(128))
            .Column<DateTime>("OccurredUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<ContactCenterEventMetricIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DateKey", column => column.NotNull().WithLength(10))
            .Column<DateTime>("Date")
            .Column<string>("EventType", column => column.NotNull().WithLength(128)),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<ContactCenterProjectionCheckpointIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("HandlerId", column => column.NotNull().WithLength(128))
            .Column<int>("Version"),
            collection: ContactCenterConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveEventAsync(
        ISession session,
        string itemId,
        string eventType,
        DateTime occurredUtc)
    {
        await session.SaveAsync(
            new InteractionEvent
            {
                ItemId = itemId,
                InteractionId = $"interaction-{itemId}",
                EventType = eventType,
                AggregateType = "Interaction",
                AggregateId = $"interaction-{itemId}",
                OccurredUtc = occurredUtc,
            },
            collection: ContactCenterConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task SaveMetricAsync(
        ISession session,
        string itemId,
        string dateKey,
        string eventType,
        DateTime date,
        long count)
    {
        await session.SaveAsync(
            new ContactCenterEventMetric
            {
                ItemId = itemId,
                DateKey = dateKey,
                Date = date,
                EventType = eventType,
                Count = count,
                CreatedUtc = date,
            },
            collection: ContactCenterConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
