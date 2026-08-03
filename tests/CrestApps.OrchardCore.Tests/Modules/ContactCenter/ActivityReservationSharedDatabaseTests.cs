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
using CrestApps.OrchardCore.Tests.Utilities;
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
    public async Task ReserveAsync_TwoProvidersReadWaitingState_OnlyOnePersistsReservation()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-reservation-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new QueueItemIndexProvider(),
            new AgentProfileIndexProvider(),
            new ActivityReservationIndexProvider(),
            new ContactCenterWorkStateIndexProvider(),
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
            await using var firstProvider = CreateServiceProvider(firstSession, readGate, distributedLock, seed);
            await using var secondProvider = CreateServiceProvider(secondSession, readGate, distributedLock, seed);

            // Act
            var firstReservationTask = firstProvider
                .GetRequiredService<IActivityReservationService>()
                .ReserveAsync(seed.QueueItem, seed.Agent, 30, TestContext.Current.CancellationToken);
            var secondReservationTask = secondProvider
                .GetRequiredService<IActivityReservationService>()
                .ReserveAsync(seed.QueueItem, seed.Agent, 30, TestContext.Current.CancellationToken);
            var attempts = await Task.WhenAll(
                CaptureReservationAttemptAsync(firstReservationTask),
                CaptureReservationAttemptAsync(secondReservationTask));

            await using var verificationSession = store.CreateSession();
            var persistedReservations = await verificationSession
                .Query<ActivityReservation, ActivityReservationIndex>(
                    index => index.ActivityItemId == seed.QueueItem.ActivityItemId,
                    collection: ContactCenterConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(attempts, attempt => attempt.Reservation is not null);
            var losingAttempt = Assert.Single(attempts, attempt => attempt.Reservation is null);

            if (losingAttempt.Exception is not null)
            {
                Assert.True(
                    losingAttempt.Exception is ConcurrencyException or System.Data.Common.DbException,
                    $"Expected a database concurrency failure but received {losingAttempt.Exception.GetType().Name}.");
            }

            Assert.Equal(4, Volatile.Read(ref lockAcquisitionCount));
            var persistedReservation = Assert.Single(persistedReservations);
            Assert.Equal(ReservationStatus.Pending, persistedReservation.Status);
            Assert.Equal(seed.QueueItem.ItemId, persistedReservation.QueueItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task ListExpiredAsync_WithKeysetCursor_PagesExpiredReservationsInStableOrder()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-expired-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            for (var i = 0; i < 5; i++)
            {
                await CreateExpiredReservationAsync(
                    store,
                    reservationId: $"reservation-{i:D2}",
                    activityId: $"activity-{i:D2}",
                    agentId: $"agent-{i:D2}",
                    expiresUtc: _now.AddMinutes(-(5 - i)));
            }

            // A future-dated pending reservation must never be returned as expired.
            await CreateReservationAsync(store, "reservation-future", "activity-future", "agent-future", ReservationStatus.Pending);

            await using var session = store.CreateSession();
            var reservationStore = new ActivityReservationStore(session);

            // Act
            var firstPage = await reservationStore.ListExpiredAsync(_now, afterExpiresUtc: null, afterDocumentId: 0, maxResults: 2, TestContext.Current.CancellationToken);
            var secondPage = await reservationStore.ListExpiredAsync(_now, firstPage.NextAfterExpiresUtc, firstPage.NextAfterDocumentId, maxResults: 2, TestContext.Current.CancellationToken);
            var thirdPage = await reservationStore.ListExpiredAsync(_now, secondPage.NextAfterExpiresUtc, secondPage.NextAfterDocumentId, maxResults: 2, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["reservation-00", "reservation-01"], firstPage.Reservations.Select(reservation => reservation.ItemId));
            Assert.True(firstPage.HasMore);
            Assert.Equal(["reservation-02", "reservation-03"], secondPage.Reservations.Select(reservation => reservation.ItemId));
            Assert.True(secondPage.HasMore);
            Assert.Equal(["reservation-04"], thirdPage.Reservations.Select(reservation => reservation.ItemId));
            Assert.False(thirdPage.HasMore);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<(ActivityReservation Reservation, Exception Exception)> CaptureReservationAttemptAsync(
        Task<ActivityReservation> reservationTask)
    {
        try
        {
            return (await reservationTask, null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    [Fact]
    public async Task AcceptAsync_WhenTheLockLeaseIsNotHonoured_OnlyOneAcceptanceIsPersisted()
    {
        // The reservation locks are taken with a fixed expiration and are never renewed, so a critical section
        // that outruns its lease continues with no lock at all while a second caller is admitted. What makes
        // that survivable is that the transition commits under a document version check rather than under the
        // lease. This test defeats the lock entirely, which is the worst case an expired lease can produce, and
        // requires that exactly one acceptance reaches the database.
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-accept-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var seed = await SeedPendingReservationAsync(store);
            var readGate = new AsyncGate(2);
            var distributedLock = CreateAlwaysGrantingLock();
            await using var firstSession = store.CreateSession();
            await using var secondSession = store.CreateSession();
            await using var firstProvider = CreateAcceptServiceProvider(firstSession, readGate, distributedLock);
            await using var secondProvider = CreateAcceptServiceProvider(secondSession, readGate, distributedLock);

            // Act
            var attempts = await Task.WhenAll(
                CaptureReservationAttemptAsync(firstProvider
                    .GetRequiredService<IActivityReservationService>()
                    .AcceptAsync(seed.ItemId, TestContext.Current.CancellationToken)),
                CaptureReservationAttemptAsync(secondProvider
                    .GetRequiredService<IActivityReservationService>()
                    .AcceptAsync(seed.ItemId, TestContext.Current.CancellationToken)));

            await using var verificationSession = store.CreateSession();
            var persisted = await CreateReservationManager(verificationSession)
                .FindByIdAsync(seed.ItemId, TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(attempts, attempt => attempt.Reservation is not null);
            var losingAttempt = Assert.Single(attempts, attempt => attempt.Reservation is null);
            Assert.NotNull(losingAttempt.Exception);
            Assert.True(
                losingAttempt.Exception is ConcurrencyException or System.Data.Common.DbException,
                $"Expected a database concurrency failure but received {losingAttempt.Exception.GetType().Name}.");
            Assert.NotNull(persisted);
            Assert.Equal(ReservationStatus.Accepted, persisted.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task ReservationIndex_DuplicateActiveActivityClaim_RejectsSecondReservation()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-activity-claim-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await CreateReservationAsync(store, "reservation-1", "activity-1", "agent-1", ReservationStatus.Pending);

            // Act
            var exception = await Record.ExceptionAsync(() =>
                CreateReservationAsync(store, "reservation-2", "activity-1", "agent-2", ReservationStatus.Pending));

            // Assert
            Assert.IsAssignableFrom<System.Data.Common.DbException>(exception);
            await CreateReservationAsync(store, "reservation-3", "activity-1", "agent-2", ReservationStatus.Rejected);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task ReservationIndex_DuplicatePendingAgentClaim_RejectsSecondReservation()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-agent-claim-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await CreateReservationAsync(store, "reservation-1", "activity-1", "agent-1", ReservationStatus.Pending);

            // Act
            var exception = await Record.ExceptionAsync(() =>
                CreateReservationAsync(store, "reservation-2", "activity-2", "agent-1", ReservationStatus.Pending));

            // Assert
            Assert.IsAssignableFrom<System.Data.Common.DbException>(exception);
            await CreateReservationAsync(store, "reservation-3", "activity-2", "agent-1", ReservationStatus.Accepted);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task QueueItemIndex_DuplicateActiveActivityClaim_RejectsSecondQueueItem()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-queue-claim-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await CreateQueueItemAsync(store, "queue-item-1", "activity-1", QueueItemStatus.Waiting);

            // Act
            var exception = await Record.ExceptionAsync(() =>
                CreateQueueItemAsync(store, "queue-item-2", "activity-1", QueueItemStatus.Reserved));

            // Assert
            Assert.IsAssignableFrom<System.Data.Common.DbException>(exception);
            await CreateQueueItemAsync(store, "queue-item-3", "activity-1", QueueItemStatus.Completed);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new QueueItemIndexProvider(),
            new AgentProfileIndexProvider(),
            new ActivityReservationIndexProvider(),
            new ContactCenterWorkStateIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);
        await CreateIndexSchemaAsync(store);

        return store;
    }

    private static async Task CreateReservationAsync(
        IStore store,
        string reservationId,
        string activityId,
        string agentId,
        ReservationStatus status)
    {
        await using var session = store.CreateSession();
        var manager = CreateReservationManager(session);
        var reservation = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        reservation.ItemId = reservationId;
        reservation.ActivityItemId = activityId;
        reservation.AgentId = agentId;
        reservation.RestorePersistedStatus(status);
        reservation.ExpiresUtc = _now.AddMinutes(1);
        await manager.CreateAsync(reservation, cancellationToken: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task CreateExpiredReservationAsync(
        IStore store,
        string reservationId,
        string activityId,
        string agentId,
        DateTime expiresUtc)
    {
        await using var session = store.CreateSession();
        var manager = CreateReservationManager(session);
        var reservation = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        reservation.ItemId = reservationId;
        reservation.ActivityItemId = activityId;
        reservation.AgentId = agentId;
        reservation.RestorePersistedStatus(ReservationStatus.Pending);
        reservation.ExpiresUtc = expiresUtc;
        await manager.CreateAsync(reservation, cancellationToken: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task CreateQueueItemAsync(
        IStore store,
        string queueItemId,
        string activityId,
        QueueItemStatus status)
    {
        await using var session = store.CreateSession();
        var manager = CreateQueueItemManager(session);
        var queueItem = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        queueItem.ItemId = queueItemId;
        queueItem.QueueId = "queue-1";
        queueItem.ActivityItemId = activityId;
        queueItem.RestorePersistedStatus(status);
        queueItem.EnqueuedUtc = _now;
        await manager.CreateAsync(queueItem, cancellationToken: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
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
            .Column<string>("ActivityClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("Priority", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull())
            .Column<DateTime>("DequeuedUtc", column => column.Nullable()),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<AgentProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("PresenceStatus", column => column.WithLength(50)),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<ContactCenterWorkStateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("AssignmentStatus", column => column.WithLength(50))
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<ActivityReservationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("AgentClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("ExpiresUtc", column => column.NotNull())
            .Column<DateTime>("ModifiedUtc", column => column.Nullable()),
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
        queueItem.RestorePersistedStatus(QueueItemStatus.Waiting);
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
        IDistributedLock distributedLock,
        (QueueItem QueueItem, AgentProfile Agent) seed)
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
        var availabilityService = new Mock<IAgentAvailabilityService>();
        availabilityService
            .Setup(service => service.GetAsync(seed.Agent.ItemId, seed.QueueItem.QueueId, It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(async (agentId, _, cancellationToken) =>
            {
                // Availability has to resolve the agent through this provider's own session. Handing back the
                // shared seed instance would give both providers the same tracked object, so the document
                // version check this test exists to exercise would never see two independent readers.
                var resolved = await agentManager.FindByIdAsync(agentId, cancellationToken);

                return resolved is null
                    ? null
                    : new AgentAvailability
                    {
                        Agent = resolved,
                    };
            });
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
        services.AddSingleton(availabilityService.Object);
        services.AddSingleton(Mock.Of<IContactCenterEventPublisher>());
        services.AddSingleton(Mock.Of<IContactCenterScopeExecutor>());
        services.AddSingleton<IEnumerable<ITelephonyService>>([]);
        services.AddSingleton(distributedLock);
        services.AddSingleton(session);
        services.AddSingleton(clock.Object);
        services.AddLogging();
        services.AddSingleton<IContactCenterWorkStateStore>(new ContactCenterWorkStateStore(session));
        services.AddSingleton<IContactCenterWorkStateManager>(provider => new ContactCenterWorkStateManager(
            provider.GetRequiredService<IContactCenterWorkStateStore>(),
            [],
            NullLogger<CatalogManager<ContactCenterWorkState>>.Instance));
        services.AddSingleton<IContactCenterWorkStateActivityProjection, ContactCenterWorkStateActivityProjection>();
        services.AddSingleton<IContactCenterWorkStateService, ContactCenterWorkStateService>();
        services.AddSingleton<IContactCenterActivityWriter, ContactCenterActivityWriter>();
        services.AddSingleton<IActivityReservationService, ActivityReservationService>();

        return services.BuildServiceProvider();
    }

    private static async Task<ActivityReservation> SeedPendingReservationAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var queueItemManager = CreateQueueItemManager(session);
        var agentManager = CreateAgentProfileManager(session);
        var reservationManager = CreateReservationManager(session);

        var agent = await agentManager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        agent.ItemId = "agent-1";
        agent.UserId = "user-1";
        agent.PresenceStatus = AgentPresenceStatus.Reserved;
        await agentManager.CreateAsync(agent, cancellationToken: TestContext.Current.CancellationToken);

        var queueItem = await queueItemManager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        queueItem.ItemId = "queue-item-1";
        queueItem.QueueId = "queue-1";
        queueItem.ActivityItemId = "activity-1";
        queueItem.AgentId = agent.ItemId;
        queueItem.RestorePersistedStatus(QueueItemStatus.Reserved);
        queueItem.EnqueuedUtc = _now;
        await queueItemManager.CreateAsync(queueItem, cancellationToken: TestContext.Current.CancellationToken);

        var reservation = await reservationManager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        reservation.ItemId = "reservation-1";
        reservation.ActivityItemId = queueItem.ActivityItemId;
        reservation.QueueId = queueItem.QueueId;
        reservation.QueueItemId = queueItem.ItemId;
        reservation.AgentId = agent.ItemId;
        reservation.RestorePersistedStatus(ReservationStatus.Pending);
        reservation.ExpiresUtc = _now.AddMinutes(1);
        await reservationManager.CreateAsync(reservation, cancellationToken: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return reservation;
    }

    private static ServiceProvider CreateAcceptServiceProvider(
        ISession session,
        AsyncGate readGate,
        IDistributedLock distributedLock)
    {
        var reservationManager = CreateReservationManager(session);
        var reservationManagerProxy = new Mock<IActivityReservationManager>();
        reservationManagerProxy
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (itemId, cancellationToken) =>
            {
                // Both callers must read the pending reservation before either commits, otherwise the second
                // would observe the accepted status and decline on its own, and the version check this test
                // exists to exercise would never run.
                var reservation = await reservationManager.FindByIdAsync(itemId, cancellationToken);
                await readGate.SignalAndWaitAsync();

                return reservation;
            });
        reservationManagerProxy
            .Setup(manager => manager.UpdateAsync(
                It.IsAny<ActivityReservation>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Returns<ActivityReservation, System.Text.Json.Nodes.JsonNode, CancellationToken>(
                (reservation, properties, cancellationToken) => reservationManager.UpdateAsync(reservation, properties, cancellationToken));

        var queueItemManager = CreateQueueItemManager(session);
        var agentManager = CreateAgentProfileManager(session);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "activity-1" });
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        var services = new ServiceCollection();
        services.AddSingleton<IActivityReservationManager>(reservationManagerProxy.Object);
        services.AddSingleton<IQueueItemManager>(queueItemManager);
        services.AddSingleton<IAgentProfileManager>(agentManager);
        services.AddSingleton(Mock.Of<IActivityQueueManager>());
        services.AddSingleton(Mock.Of<IActivityQueueService>());
        services.AddSingleton(Mock.Of<IInteractionManager>());
        services.AddSingleton(Mock.Of<IAgentAvailabilityService>());
        services.AddSingleton(activityManager.Object);
        services.AddSingleton(Mock.Of<IContactCenterEventPublisher>());
        services.AddSingleton(Mock.Of<IContactCenterScopeExecutor>());
        services.AddSingleton<IEnumerable<ITelephonyService>>([]);
        services.AddSingleton(distributedLock);
        services.AddSingleton(session);
        services.AddSingleton(clock.Object);
        services.AddLogging();
        services.AddSingleton<IContactCenterWorkStateStore>(new ContactCenterWorkStateStore(session));
        services.AddSingleton<IContactCenterWorkStateManager>(provider => new ContactCenterWorkStateManager(
            provider.GetRequiredService<IContactCenterWorkStateStore>(),
            [],
            NullLogger<CatalogManager<ContactCenterWorkState>>.Instance));
        services.AddSingleton<IContactCenterWorkStateActivityProjection, ContactCenterWorkStateActivityProjection>();
        services.AddSingleton<IContactCenterWorkStateService, ContactCenterWorkStateService>();
        services.AddSingleton<IContactCenterActivityWriter, ContactCenterActivityWriter>();
        services.AddSingleton<IActivityReservationService, ActivityReservationService>();

        return services.BuildServiceProvider();
    }

    private static IDistributedLock CreateAlwaysGrantingLock()
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        return distributedLock.Object;
    }

    private static IDistributedLock CreateOverlappingLock(Action onAcquired)
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(
                It.Is<string>(key =>
                    key == "ContactCenterActivityReservation:activity-1" ||
                    key == "ContactCenterAgentReservation:agent-1"),
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
