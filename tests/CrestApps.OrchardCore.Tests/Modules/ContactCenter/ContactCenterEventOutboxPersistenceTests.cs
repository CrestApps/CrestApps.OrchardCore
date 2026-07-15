using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterEventOutboxPersistenceTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PublishAsync_WhenSessionIsNotCommitted_RollsBackDomainEventAndOutbox()
    {
        // Arrange
        var databasePath = DatabasePath("rollback");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var session = store.CreateSession())
            {
                var publisher = CreatePublisher(session);
                session.Save(
                    new QueueItem
                    {
                        ItemId = "queue-item-1",
                        ActivityItemId = "activity-1",
                        QueueId = "queue-1",
                    },
                    collection: ContactCenterConstants.CollectionName);
                await publisher.PublishAsync(
                    CreateEvent(),
                    TestContext.Current.CancellationToken);
            }

            await using var verificationSession = store.CreateSession();
            var queueItem = await verificationSession
                .Query<QueueItem>(collection: ContactCenterConstants.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            var interactionEvent = await verificationSession
                .Query<InteractionEvent>(collection: ContactCenterConstants.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            var outboxMessage = await verificationSession
                .Query<ContactCenterOutboxMessage>(collection: ContactCenterConstants.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(queueItem);
            Assert.Null(interactionEvent);
            Assert.Null(outboxMessage);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task PublishAsync_WhenSessionCommits_PersistsDomainEventAndOutboxTogether()
    {
        // Arrange
        var databasePath = DatabasePath("commit");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var session = store.CreateSession())
            {
                var publisher = CreatePublisher(session);
                session.Save(
                    new QueueItem
                    {
                        ItemId = "queue-item-1",
                        ActivityItemId = "activity-1",
                        QueueId = "queue-1",
                    },
                    collection: ContactCenterConstants.CollectionName);
                await publisher.PublishAsync(
                    CreateEvent(),
                    TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var verificationSession = store.CreateSession();
            var queueItem = await verificationSession
                .Query<QueueItem>(collection: ContactCenterConstants.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            var interactionEvent = await verificationSession
                .Query<InteractionEvent>(collection: ContactCenterConstants.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            var outboxMessage = await verificationSession
                .Query<ContactCenterOutboxMessage>(collection: ContactCenterConstants.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(queueItem);
            Assert.NotNull(interactionEvent);
            Assert.NotNull(outboxMessage);
            Assert.Equal(interactionEvent.ItemId, outboxMessage.EventId);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task PublishAsync_WhenTwoSessionsUseSameIdempotencyKey_OnlyOneTransactionCommits()
    {
        // Arrange
        var databasePath = DatabasePath("duplicate");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var firstSession = store.CreateSession();
            await using var secondSession = store.CreateSession();
            firstSession.Save(
                new QueueItem { ItemId = "queue-item-1", ActivityItemId = "activity-1", QueueId = "queue-1" },
                collection: ContactCenterConstants.CollectionName);
            secondSession.Save(
                new QueueItem { ItemId = "queue-item-2", ActivityItemId = "activity-2", QueueId = "queue-1" },
                collection: ContactCenterConstants.CollectionName);
            await StageEventAndOutboxAsync(firstSession, CreateEvent(), TestContext.Current.CancellationToken);
            await StageEventAndOutboxAsync(secondSession, CreateEvent(), TestContext.Current.CancellationToken);

            // Act
            await firstSession.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            await Assert.ThrowsAnyAsync<DbException>(() =>
                secondSession.SaveChangesAsync(TestContext.Current.CancellationToken));

            await using var verificationSession = store.CreateSession();
            var queueItems = await verificationSession
                .Query<QueueItem>(collection: ContactCenterConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);
            var interactionEvents = await verificationSession
                .Query<InteractionEvent>(collection: ContactCenterConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);
            var outboxMessages = await verificationSession
                .Query<ContactCenterOutboxMessage>(collection: ContactCenterConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            Assert.Single(queueItems);
            Assert.Single(interactionEvents);
            Assert.Single(outboxMessages);
            Assert.Equal(interactionEvents.Single().ItemId, outboxMessages.Single().EventId);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static InteractionEvent CreateEvent()
    {
        return new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.AgentReserved,
            InteractionId = "interaction-1",
            IdempotencyKey = "event-key-1",
        };
    }

    private static DefaultContactCenterEventPublisher CreatePublisher(ISession session)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        var eventStore = new InteractionEventStore(session);
        var outbox = new ContactCenterOutbox(
            [],
            new ContactCenterOutboxStore(session),
            eventStore,
            scopeExecutor.Object,
            session,
            clock.Object,
            NullLogger<ContactCenterOutbox>.Instance);

        return new DefaultContactCenterEventPublisher(
            eventStore,
            outbox,
            scopeExecutor.Object,
            clock.Object,
            NullLogger<DefaultContactCenterEventPublisher>.Instance);
    }

    private static async Task StageEventAndOutboxAsync(
        ISession session,
        InteractionEvent interactionEvent,
        CancellationToken cancellationToken)
    {
        interactionEvent.ItemId = IdGenerator.GenerateId();
        interactionEvent.OccurredUtc = _now;
        interactionEvent.SchemaVersion = ContactCenterConstants.CurrentEventSchemaVersion;
        interactionEvent.ActorId = ContactCenterConstants.SystemActor;
        var eventStore = new InteractionEventStore(session);
        var outboxStore = new ContactCenterOutboxStore(session);

        await eventStore.CreateAsync(interactionEvent, cancellationToken);
        await outboxStore.CreateAsync(
            new ContactCenterOutboxMessage
            {
                ItemId = IdGenerator.GenerateId(),
                EventId = interactionEvent.ItemId,
                EventType = interactionEvent.EventType,
                Status = OutboxMessageStatus.Pending,
                NextAttemptUtc = _now,
                CreatedUtc = _now,
                ModifiedUtc = _now,
            },
            cancellationToken);
    }

    private static string DatabasePath(string suffix)
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"contact-center-event-outbox-{suffix}-{Guid.NewGuid():N}.db");
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new InteractionEventIndexProvider(),
            new ContactCenterOutboxMessageIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            ContactCenterConstants.CollectionName,
            TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        var eventMigration = new InteractionEventIndexMigrations(store)
        {
            SchemaBuilder = schemaBuilder,
        };
        var outboxMigration = new ContactCenterOutboxMessageIndexMigrations
        {
            SchemaBuilder = schemaBuilder,
        };

        await eventMigration.CreateAsync();
        await outboxMigration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
