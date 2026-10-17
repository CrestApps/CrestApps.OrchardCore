using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The queue items saved before the queue item index carried the dialer profile get it indexed after the upgrade, so the
/// dialer queue inventory groups them by the profile they are stamped with.
/// </summary>
public sealed class QueueItemDialerProfileIndexBackfillBackgroundTaskTests
{
    private static readonly DateTime _now = new(2026, 10, 16, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DoWorkAsync_ItemsSavedBeforeTheColumn_GetTheirStampedProfileIndexed()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-queue-item-profile-backfill-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveItemAsync(seedSession, "waiting", QueueItemStatus.Waiting, "profile-1");
                await SaveItemAsync(seedSession, "reserved", QueueItemStatus.Reserved, "profile-2");
                await SaveItemAsync(seedSession, "inbound", QueueItemStatus.Waiting, null);
                await SaveItemAsync(seedSession, "settled", QueueItemStatus.Completed, "profile-1");
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await ClearIndexedProfilesAsync(store);

            await using var services = new ServiceCollection()
                .AddSingleton(store)
                .AddSingleton<IContactCenterFeatureWorkManager>(new TestContactCenterFeatureWorkManager())
                .AddLogging()
                .BuildServiceProvider();

            var task = new QueueItemDialerProfileIndexBackfillBackgroundTask();

            // Act
            await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

            // Assert
            var indexed = await ReadIndexedProfilesAsync(store);

            Assert.Equal("profile-1", indexed["waiting"]);
            Assert.Equal("profile-2", indexed["reserved"]);
            Assert.Null(indexed["inbound"]);

            // A settled item is never read by the inventory, so it is left as it was.
            Assert.Null(indexed["settled"]);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpdateFrom6Async_AddsTheColumnAndNewItemsAreIndexedWithTheirProfile()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-queue-item-profile-column-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            // Act
            await using (var seedSession = store.CreateSession())
            {
                await SaveItemAsync(seedSession, "loaded", QueueItemStatus.Waiting, "profile-1");
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            var indexed = await ReadIndexedProfilesAsync(store);

            Assert.Equal("profile-1", indexed["loaded"]);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new QueueItemIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new QueueItemIndexMigrations(store, new StubClock(_now))
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        };

        await migration.CreateAsync();
        await migration.UpdateFrom3Async();

        Assert.Equal(7, await migration.UpdateFrom6Async());

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveItemAsync(ISession session, string itemId, QueueItemStatus status, string dialerProfileId)
    {
        await session.SaveAsync(
            new QueueItem
            {
                ItemId = itemId,
                QueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1"),
                ActivityItemId = $"activity-{itemId}",
                DialerProfileId = dialerProfileId,
                EnqueuedUtc = _now,
            }.RestorePersistedStatus(status),
            collection: ContactCenterStorage.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    // Puts the index back to how a release before the column left it: every row without a dialer profile.
    private static async Task ClearIndexedProfilesAsync(IStore store)
    {
        var configuration = store.Configuration;
        var table = configuration.SqlDialect.QuoteForTableName(
            configuration.TablePrefix + QueueItemQueryPlanFixture.TableName(configuration),
            configuration.Schema);
        var column = configuration.SqlDialect.QuoteForColumnName(nameof(QueueItemIndex.DialerProfileId));

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await using (var command = transaction.Connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"UPDATE {table} SET {column} = NULL";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Dictionary<string, string>> ReadIndexedProfilesAsync(IStore store)
    {
        await using var session = store.CreateSession();

        var rows = await session.QueryIndex<QueueItemIndex>(index => index.ItemId != null, collection: ContactCenterStorage.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken);

        return rows.ToDictionary(row => row.ItemId, row => row.DialerProfileId, StringComparer.Ordinal);
    }
}
