using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ActivityReservationSharedDatabaseTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReserveAsync_TwoProvidersReadWaitingState_BothPersistReservationsToday()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-reservation-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new QueueItemIndexProvider(),
            new AgentProfileIndexProvider(),
            new ActivityReservationIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);
        await CreateIndexSchemaAsync(store);

        try
        {
            var seed = await SeedAsync(store);
            var readGate = new AsyncGate(2);
            var lockAcquisitionCount = 0;
            var distributedLock = CreateOverlappingLock(() => Interlocked.Increment(ref lockAcquisitionCount));
            await using var firstSession = store.CreateSession();
            await using var secondSession = store.CreateSession();
            await using var firstProvider = CreateServiceProvider(firstSession, readGate, distributedLock);
            await using var secondProvider = CreateServiceProvider(secondSession, readGate, distributedLock);

            // Act
            var firstReservationTask = firstProvider
                .GetRequiredService<IActivityReservationService>()
                .ReserveAsync(seed.QueueItem, seed.Agent, 30, TestContext.Current.CancellationToken);
            var secondReservationTask = secondProvider
                .GetRequiredService<IActivityReservationService>()
                .ReserveAsync(seed.QueueItem, seed.Agent, 30, TestContext.Current.CancellationToken);
            var reservations = await Task.WhenAll(firstReservationTask, secondReservationTask);

            await firstSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            await secondSession.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var verificationSession = store.CreateSession();
            var persistedReservations = await verificationSession
                .Query<ActivityReservation, ActivityReservationIndex>(
                    index => index.ActivityItemId == seed.QueueItem.ActivityItemId,
                    collection: ContactCenterConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.All(reservations, Assert.NotNull);
            Assert.Equal(2, Volatile.Read(ref lockAcquisitionCount));
            Assert.Equal(2, persistedReservations.Count());
            Assert.All(persistedReservations, reservation =>
            {
                Assert.Equal(ReservationStatus.Pending, reservation.Status);
                Assert.Equal(seed.QueueItem.ItemId, reservation.QueueItemId);
            });
            Assert.Equal(2, persistedReservations.Select(reservation => reservation.ItemId).Distinct().Count());
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static async Task CreateIndexSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<QueueItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("Priority", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<AgentProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("PresenceStatus", column => column.WithLength(50)),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<ActivityReservationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("ExpiresUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<(QueueItem QueueItem, AgentProfile Agent)> SeedAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var queueItemManager = CreateQueueItemManager(session);
        var agentManager = CreateAgentProfileManager(session);
        var queueItem = await queueItemManager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        queueItem.ItemId = "queue-item-1";
        queueItem.QueueId = "queue-1";
        queueItem.ActivityItemId = "activity-1";
        queueItem.Status = QueueItemStatus.Waiting;
        await queueItemManager.CreateAsync(queueItem, cancellationToken: TestContext.Current.CancellationToken);

        var agent = await agentManager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        agent.ItemId = "agent-1";
        agent.UserId = "user-1";
        agent.PresenceStatus = AgentPresenceStatus.Available;
        await agentManager.CreateAsync(agent, cancellationToken: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (queueItem, agent);
    }

    private static ServiceProvider CreateServiceProvider(
        ISession session,
        AsyncGate readGate,
        IDistributedLock distributedLock)
    {
        var queueItemManager = CreateQueueItemManager(session);
        var queueItemManagerProxy = new Mock<IQueueItemManager>();
        queueItemManagerProxy
            .Setup(manager => manager.FindByIdAsync("queue-item-1", It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (itemId, cancellationToken) =>
            {
                var item = await queueItemManager.FindByIdAsync(itemId, cancellationToken);
                await readGate.SignalAndWaitAsync();

                return item;
            });
        queueItemManagerProxy
            .Setup(manager => manager.UpdateAsync(
                It.IsAny<QueueItem>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Returns<QueueItem, System.Text.Json.Nodes.JsonNode, CancellationToken>(
                (item, properties, cancellationToken) => queueItemManager.UpdateAsync(item, properties, cancellationToken));

        var agentManager = CreateAgentProfileManager(session);
        var reservationManager = CreateReservationManager(session);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "activity-1" });
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        var services = new ServiceCollection();
        services.AddSingleton(reservationManager);
        services.AddSingleton<IActivityReservationManager>(reservationManager);
        services.AddSingleton(queueItemManagerProxy.Object);
        services.AddSingleton<IQueueItemManager>(queueItemManagerProxy.Object);
        services.AddSingleton(agentManager);
        services.AddSingleton<IAgentProfileManager>(agentManager);
        services.AddSingleton(Mock.Of<IActivityQueueManager>());
        services.AddSingleton(Mock.Of<IActivityQueueService>());
        services.AddSingleton(Mock.Of<IInteractionManager>());
        services.AddSingleton(activityManager.Object);
        services.AddSingleton(Mock.Of<IContactCenterEventPublisher>());
        services.AddSingleton<IEnumerable<ITelephonyService>>([]);
        services.AddSingleton(distributedLock);
        services.AddSingleton(clock.Object);
        services.AddLogging();
        services.AddSingleton<IActivityReservationService, ActivityReservationService>();

        return services.BuildServiceProvider();
    }

    private static IDistributedLock CreateOverlappingLock(Action onAcquired)
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(
                "ContactCenterAgentReservation:agent-1",
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan?>()))
            .Callback(onAcquired)
            .ReturnsAsync((null, true));

        return distributedLock.Object;
    }

    private static QueueItemManager CreateQueueItemManager(ISession session)
    {
        return new QueueItemManager(
            new QueueItemStore(session),
            [],
            NullLogger<CatalogManager<QueueItem>>.Instance);
    }

    private static AgentProfileManager CreateAgentProfileManager(ISession session)
    {
        return new AgentProfileManager(
            new AgentProfileStore(session),
            [],
            NullLogger<CatalogManager<AgentProfile>>.Instance);
    }

    private static ActivityReservationManager CreateReservationManager(ISession session)
    {
        return new ActivityReservationManager(
            new ActivityReservationStore(session),
            [],
            NullLogger<CatalogManager<ActivityReservation>>.Instance);
    }

    private sealed class AsyncGate
    {
        private readonly int _participantCount;
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public AsyncGate(int participantCount)
        {
            _participantCount = participantCount;
        }

        public Task SignalAndWaitAsync()
        {
            if (Interlocked.Increment(ref _arrivals) == _participantCount)
            {
                _completion.TrySetResult();
            }

            return _completion.Task;
        }
    }

}
