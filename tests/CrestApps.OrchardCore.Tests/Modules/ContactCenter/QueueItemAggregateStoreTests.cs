using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class QueueItemAggregateStoreTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    private const string TargetQueueId = "queue-target";
    private const string OtherQueueId = "queue-other";

    [Fact]
    public async Task CountWaitingAsync_CountsOnlyWaitingItemsInQueue()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-queue-count-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveItemAsync(seedSession, "w1", TargetQueueId, QueueItemStatus.Waiting, _now.AddSeconds(-100));
                await SaveItemAsync(seedSession, "w2", TargetQueueId, QueueItemStatus.Waiting, _now.AddSeconds(-50));
                await SaveItemAsync(seedSession, "w3", TargetQueueId, QueueItemStatus.Waiting, _now.AddSeconds(-10));
                await SaveItemAsync(seedSession, "reserved", TargetQueueId, QueueItemStatus.Reserved, _now.AddSeconds(-30));
                await SaveItemAsync(seedSession, "other", OtherQueueId, QueueItemStatus.Waiting, _now.AddSeconds(-5));
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var itemStore = new QueueItemStore(querySession);

            // Act
            var targetCount = await itemStore.CountWaitingAsync(TargetQueueId, TestContext.Current.CancellationToken);
            var otherCount = await itemStore.CountWaitingAsync(OtherQueueId, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(3, targetCount);
            Assert.Equal(1, otherCount);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task FindLongestWaitingAsync_ReturnsOldestWaitingItem()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-queue-longest-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveItemAsync(seedSession, "newer", TargetQueueId, QueueItemStatus.Waiting, _now.AddSeconds(-10));
                await SaveItemAsync(seedSession, "oldest", TargetQueueId, QueueItemStatus.Waiting, _now.AddSeconds(-100));
                await SaveItemAsync(seedSession, "reserved-older", TargetQueueId, QueueItemStatus.Reserved, _now.AddSeconds(-500));
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var itemStore = new QueueItemStore(querySession);

            // Act
            var longest = await itemStore.FindLongestWaitingAsync(TargetQueueId, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(longest);
            Assert.Equal("oldest", longest.ItemId);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CountWaitingOlderThanAsync_CountsOnlyWaitingItemsEnqueuedBeforeThreshold()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-queue-breach-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveItemAsync(seedSession, "breached", TargetQueueId, QueueItemStatus.Waiting, _now.AddSeconds(-100));
                await SaveItemAsync(seedSession, "within", TargetQueueId, QueueItemStatus.Waiting, _now.AddSeconds(-50));
                await SaveItemAsync(seedSession, "fresh", TargetQueueId, QueueItemStatus.Waiting, _now.AddSeconds(-10));
                await SaveItemAsync(seedSession, "reserved-old", TargetQueueId, QueueItemStatus.Reserved, _now.AddSeconds(-500));
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var itemStore = new QueueItemStore(querySession);

            // Act
            var breachCount = await itemStore.CountWaitingOlderThanAsync(
                TargetQueueId,
                _now.AddSeconds(-60),
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, breachCount);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new QueueItemIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<QueueItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("Priority", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveItemAsync(
        ISession session,
        string itemId,
        string queueId,
        QueueItemStatus status,
        DateTime enqueuedUtc)
    {
        await session.SaveAsync(
            new QueueItem
            {
                ItemId = itemId,
                QueueId = queueId,
                ActivityItemId = $"activity-{itemId}",
                Status = status,
                Priority = InteractionPriority.Normal,
                EnqueuedUtc = enqueuedUtc,
            },
            collection: ContactCenterConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
