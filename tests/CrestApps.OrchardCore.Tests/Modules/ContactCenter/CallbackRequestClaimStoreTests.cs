using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class CallbackRequestClaimStoreTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListDueAsync_ReturnsOnlyPendingDueUnleasedItems()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-callback-due-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveAsync(seedSession, "due-unleased", CallbackRequestStatus.Pending, _now.AddMinutes(-5), leaseExpiresUtc: null);
                await SaveAsync(seedSession, "due-lease-expired", CallbackRequestStatus.Pending, _now.AddMinutes(-4), leaseExpiresUtc: _now.AddMinutes(-1));
                await SaveAsync(seedSession, "due-lease-active", CallbackRequestStatus.Pending, _now.AddMinutes(-3), leaseExpiresUtc: _now.AddMinutes(5));
                await SaveAsync(seedSession, "future", CallbackRequestStatus.Pending, _now.AddMinutes(10), leaseExpiresUtc: null);
                await SaveAsync(seedSession, "scheduled", CallbackRequestStatus.Scheduled, _now.AddMinutes(-5), leaseExpiresUtc: null);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var callbackStore = new CallbackRequestStore(querySession);

            // Act
            var due = await callbackStore.ListDueAsync(_now, maxCount: 10, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, due.Count);
            Assert.Contains(due, callback => callback.ItemId == "due-unleased");
            Assert.Contains(due, callback => callback.ItemId == "due-lease-expired");
            Assert.DoesNotContain(due, callback => callback.ItemId == "due-lease-active");
            Assert.DoesNotContain(due, callback => callback.ItemId == "future");
            Assert.DoesNotContain(due, callback => callback.ItemId == "scheduled");
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ListDueAsync_BoundsResultsToMaxCountOrderedByScheduledUtc()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-callback-bound-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveAsync(seedSession, "oldest", CallbackRequestStatus.Pending, _now.AddMinutes(-30), leaseExpiresUtc: null);
                await SaveAsync(seedSession, "middle", CallbackRequestStatus.Pending, _now.AddMinutes(-20), leaseExpiresUtc: null);
                await SaveAsync(seedSession, "newest", CallbackRequestStatus.Pending, _now.AddMinutes(-10), leaseExpiresUtc: null);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var callbackStore = new CallbackRequestStore(querySession);

            // Act
            var due = await callbackStore.ListDueAsync(_now, maxCount: 2, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, due.Count);
            Assert.Equal("oldest", due.First().ItemId);
            Assert.DoesNotContain(due, callback => callback.ItemId == "newest");
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
        store.RegisterIndexes([new CallbackRequestIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<CallbackRequestIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("ScheduledUtc")
            .Column<DateTime>("LeaseExpiresUtc", column => column.Nullable()),
            collection: ContactCenterConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveAsync(
        ISession session,
        string itemId,
        CallbackRequestStatus status,
        DateTime scheduledUtc,
        DateTime? leaseExpiresUtc)
    {
        await session.SaveAsync(
            new CallbackRequest
            {
                ItemId = itemId,
                Destination = "+15551234567",
                Status = status,
                ScheduledUtc = scheduledUtc,
                LeaseExpiresUtc = leaseExpiresUtc,
            },
            collection: ContactCenterConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
