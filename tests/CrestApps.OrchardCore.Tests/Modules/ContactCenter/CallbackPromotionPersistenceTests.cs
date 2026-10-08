using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Due callbacks promoted by the real <see cref="CallbackService"/> over the real callback store on SQLite, with the
/// session committed mid-pass exactly where enqueuing commits it in production.
/// </summary>
public sealed class CallbackPromotionPersistenceTests
{
    private static readonly DateTime _now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    // Live (2026-09-27), one callback a caller asked for became two preview calls a minute apart. Promoting the first due
    // callback committed the session (enqueuing saves it), a commit detaches everything the session read before it, and
    // saving the second callback from that batch read INSERTed it as a second, still-pending row the next pass promoted
    // again. Confirmed fixed live: each callback is read again on its own before it is claimed.
    [Fact]
    public async Task PromoteDueAsync_TwoDueCallbacks_BecomeOneActivityEach_AndNoCallbackIsStoredTwice()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-callback-promotion-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveAsync(seedSession, "cb-1", "+15550000001", _now.AddMinutes(-2));
                await SaveAsync(seedSession, "cb-2", "+15550000002", _now.AddMinutes(-1));
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var activities = new List<OmnichannelActivity>();
            int promoted;
            int promotedAgain;

            await using (var session = store.CreateSession())
            {
                var service = CreateService(session, activities);

                // Act
                promoted = await service.PromoteDueAsync(TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);

                // The next background pass finds nothing left to promote.
                promotedAgain = await service.PromoteDueAsync(TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            Assert.Equal(2, promoted);
            Assert.Equal(0, promotedAgain);

            await using var readSession = store.CreateSession();
            var rows = (await readSession
                .Query<CallbackRequest, CallbackRequestIndex>(collection: ContactCenterStorage.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken))
                .OrderBy(callback => callback.ItemId, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(["cb-1", "cb-2"], rows.Select(callback => callback.ItemId));
            Assert.All(rows, callback => Assert.Equal(CallbackRequestStatus.Scheduled, callback.Status));

            Assert.Equal(2, activities.Count);
            Assert.Equal(["+15550000001", "+15550000002"], activities.Select(activity => activity.PreferredDestination).Order(StringComparer.Ordinal));
            Assert.All(rows, callback => Assert.Single(activities, activity => activity.ItemId == callback.ActivityItemId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static CallbackService CreateService(ISession session, List<OmnichannelActivity> activities)
    {
        var callbackManager = new CallbackRequestManager(
            new CallbackRequestStore(session),
            [],
            NullLogger<CatalogManager<CallbackRequest>>.Instance);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new OmnichannelActivity { ItemId = $"activity-{activities.Count + 1}" });
        activityManager
            .Setup(manager => manager.CreateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()))
            .Callback<OmnichannelActivity, CancellationToken>((activity, _) => activities.Add(activity))
            .Returns(ValueTask.CompletedTask);

        var workStates = new Mock<IContactCenterWorkStateService>();
        workStates
            .Setup(service => service.MutateAsync(It.IsAny<string>(), It.IsAny<Action<ContactCenterWorkState>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterWorkState());

        // ActivityQueueService.EnqueueAsync commits the session so routing sees the new queue item at once. That commit,
        // in the middle of the pass, is what detached the rest of the batch.
        var queues = new Mock<IActivityQueueService>();
        queues
            .Setup(service => service.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, InteractionPriority? _, CancellationToken cancellationToken) =>
            {
                await session.SaveChangesAsync(cancellationToken);

                return (QueueItem)null;
            });

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new CallbackService(
            callbackManager,
            activityManager.Object,
            workStates.Object,
            queues.Object,
            Mock.Of<IContactCenterEventPublisher>(),
            clock.Object);
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new CallbackRequestIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<CallbackRequestIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("ScheduledUtc")
            .Column<DateTime>("LeaseExpiresUtc", column => column.Nullable())
            .Column<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ContactCenterStorage.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveAsync(ISession session, string itemId, string destination, DateTime scheduledUtc)
    {
        await session.SaveAsync(
            new CallbackRequest
            {
                ItemId = itemId,
                Destination = destination,
                QueueId = "queue-1",
                Status = CallbackRequestStatus.Pending,
                RequestedUtc = scheduledUtc,
                ScheduledUtc = scheduledUtc,
            },
            collection: ContactCenterStorage.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
