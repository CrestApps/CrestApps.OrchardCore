using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterQueueBacklogHealthCheckTests
{
    private static readonly DateTime _now = new(2026, 7, 21, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CountAllWaitingAsync_CountsWaitingItemsAcrossEveryQueue()
    {
        // Arrange
        var databasePath = DatabasePath("queue-backlog-count");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveItemAsync(seedSession, "w1", "queue-a", QueueItemStatus.Waiting);
                await SaveItemAsync(seedSession, "w2", "queue-a", QueueItemStatus.Waiting);
                await SaveItemAsync(seedSession, "w3", "queue-b", QueueItemStatus.Waiting);
                await SaveItemAsync(seedSession, "reserved", "queue-a", QueueItemStatus.Reserved);
                await SaveItemAsync(seedSession, "assigned", "queue-b", QueueItemStatus.Assigned);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var assertSession = store.CreateSession();
            var itemStore = new QueueItemStore(assertSession);

            var waiting = await itemStore.CountAllWaitingAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(3, waiting);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_ReportsQueuedInteractionCountAsHealthyData()
    {
        // Arrange
        var databasePath = DatabasePath("queue-backlog-health");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveItemAsync(seedSession, "w1", "queue-a", QueueItemStatus.Waiting);
                await SaveItemAsync(seedSession, "w2", "queue-b", QueueItemStatus.Waiting);
                await SaveItemAsync(seedSession, "assigned", "queue-a", QueueItemStatus.Assigned);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var assertSession = store.CreateSession();
            var check = new ContactCenterQueueBacklogHealthCheck(new QueueItemStore(assertSession));

            // Act
            var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal(2, Assert.IsType<int>(result.Data["queued_interactions"]));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"contact-center-{prefix}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new QueueItemIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new QueueItemIndexMigrations(store, new StubClock())
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        };

        await migration.CreateAsync();
        await migration.UpdateFrom3Async();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveItemAsync(
        ISession session,
        string itemId,
        string queueId,
        QueueItemStatus status)
    {
        await session.SaveAsync(
            new QueueItem
            {
                ItemId = itemId,
                QueueId = queueId,
                ActivityItemId = $"activity-{itemId}",
                Priority = InteractionPriority.Normal,
                EnqueuedUtc = _now,
            }.RestorePersistedStatus(status),
            collection: ContactCenterStorage.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
