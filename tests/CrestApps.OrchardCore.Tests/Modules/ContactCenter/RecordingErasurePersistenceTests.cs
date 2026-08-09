using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class RecordingErasurePersistenceTests
{
    private static readonly DateTime _erasedUtc = new(2031, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    [Fact]
    public async Task EraseAsync_WhenOutboxEnqueueFails_RollsBackPointersTombstoneAndDomainEvent()
    {
        // Arrange
        var databasePath = DatabasePath("rollback");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store);

            await using (var session = store.CreateSession())
            {
                var outbox = new Mock<IContactCenterOutbox>();
                outbox
                    .Setup(value => value.EnqueueAsync(
                        It.IsAny<InteractionEvent>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("outbox unavailable"));
                var service = CreateService(session, outbox.Object);

                // Act
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.EraseAsync(
                        "interaction-1",
                        "dpo-1",
                        "gdpr-subject-request",
                        TestContext.Current.CancellationToken));
            }

            // Assert
            await using var verification = store.CreateSession();
            var interaction = await verification
                .Query<Interaction, InteractionIndex>(
                    index => index.ItemId == "interaction-1",
                    collection: ContactCenterStorage.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            var callSession = await verification
                .Query<CallSession, CallSessionIndex>(
                    index => index.InteractionId == "interaction-1",
                    collection: ContactCenterStorage.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            var events = await verification
                .Query<InteractionEvent, InteractionEventIndex>(
                    collection: ContactCenterStorage.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal("storage/interaction-1", interaction.RecordingReference);
            Assert.Null(interaction.RecordingErasedUtc);
            Assert.Equal("storage/interaction-1", callSession.RecordingReference);
            Assert.Empty(events);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task EraseAsync_WhenUnitOfWorkCommits_PersistsPointersTombstoneEventAndOutboxTogether()
    {
        // Arrange
        var databasePath = DatabasePath("commit");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store);

            await using (var session = store.CreateSession())
            {
                var publisher = CreatePublisher(session);
                var service = CreateService(session, publisher);

                // Act
                var decision = await service.EraseAsync(
                    "interaction-1",
                    "dpo-1",
                    "gdpr-subject-request",
                    TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);

                Assert.True(decision.Erased);
            }

            // Assert
            await using var verification = store.CreateSession();
            var interaction = await verification
                .Query<Interaction, InteractionIndex>(
                    index => index.ItemId == "interaction-1",
                    collection: ContactCenterStorage.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            var callSession = await verification
                .Query<CallSession, CallSessionIndex>(
                    index => index.InteractionId == "interaction-1",
                    collection: ContactCenterStorage.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            var interactionEvent = await verification
                .Query<InteractionEvent, InteractionEventIndex>(
                    index => index.EventType == ContactCenterConstants.Events.RecordingErased,
                    collection: ContactCenterStorage.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            var outbox = await verification
                .Query<ContactCenterOutboxMessage, ContactCenterOutboxMessageIndex>(
                    index => index.EventId == interactionEvent.ItemId,
                    collection: ContactCenterStorage.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.Null(interaction.RecordingReference);
            Assert.Equal(_erasedUtc, interaction.RecordingErasedUtc);
            Assert.Null(callSession.RecordingReference);
            Assert.NotNull(interactionEvent);
            Assert.NotNull(outbox);

            var data = interactionEvent.GetData<RecordingErasedEventData>();

            Assert.Equal("storage/interaction-1", data.RecordingReference);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static RecordingAccessGovernanceService CreateService(
        ISession session,
        IContactCenterOutbox outbox)
    {
        var eventStore = new InteractionEventStore(session, new DefaultInteractionEventUpcastService([]));
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        var publisher = new DefaultContactCenterEventPublisher(
            eventStore,
            outbox,
            scopeExecutor.Object,
            new StubClock(_erasedUtc),
            NullLogger<DefaultContactCenterEventPublisher>.Instance);

        return CreateService(session, publisher);
    }

    private static RecordingAccessGovernanceService CreateService(
        ISession session,
        IContactCenterEventPublisher publisher)
    {
        var interactionManager = new InteractionManager(
            new InteractionStore(session),
            [],
            NullLogger<CatalogManager<Interaction>>.Instance);
        var callSessionManager = new CallSessionManager(
            new CallSessionStore(session),
            [],
            NullLogger<CatalogManager<CallSession>>.Instance);

        return new RecordingAccessGovernanceService(
            interactionManager,
            callSessionManager,
            publisher,
            new StubClock(_erasedUtc));
    }

    private static DefaultContactCenterEventPublisher CreatePublisher(ISession session)
    {
        var eventStore = new InteractionEventStore(session, new DefaultInteractionEventUpcastService([]));
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        var outbox = new ContactCenterOutbox(
            [],
            new ContactCenterOutboxStore(session),
            eventStore,
            scopeExecutor.Object,
            new TestContactCenterFeatureWorkManager(),
            session,
            new StubClock(_erasedUtc),
            NullLogger<ContactCenterOutbox>.Instance);

        return new DefaultContactCenterEventPublisher(
            eventStore,
            outbox,
            scopeExecutor.Object,
            new StubClock(_erasedUtc),
            NullLogger<DefaultContactCenterEventPublisher>.Instance);
    }

    private static async Task SeedAsync(IStore store)
    {
        await using var session = store.CreateSession();
        await session.SaveAsync(
            new Interaction
            {
                ItemId = "interaction-1",
                ProviderName = "Asterisk",
                ProviderInteractionId = "provider-call-1",
                RecordingReference = "storage/interaction-1",
            },
            collection: ContactCenterStorage.CollectionName);
        await session.SaveAsync(
            new CallSession
            {
                ItemId = "call-session-1",
                InteractionId = "interaction-1",
                ProviderName = "Asterisk",
                ProviderCallId = "provider-call-1",
                RecordingReference = "storage/interaction-1",
                CreatedUtc = _erasedUtc.AddHours(-1),
            },
            collection: ContactCenterStorage.CollectionName);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static string DatabasePath(string suffix)
        => Path.Combine(Path.GetTempPath(), $"recording-erasure-{suffix}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var identityResolver = new ProviderIdentityResolver([]);
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        store.RegisterIndexes(
        [
            new InteractionIndexProvider(),
            new CallSessionIndexProvider(identityResolver),
            new InteractionEventIndexProvider(),
            new ContactCenterOutboxMessageIndexProvider(),
        ]);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            ContactCenterStorage.CollectionName,
            TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        var interactionMigration = new InteractionIndexMigrations
        {
            SchemaBuilder = schemaBuilder,
        };
        var callSessionMigration = new CallSessionIndexMigrations(store, identityResolver)
        {
            SchemaBuilder = schemaBuilder,
        };
        var eventMigration = new InteractionEventIndexMigrations(store)
        {
            SchemaBuilder = schemaBuilder,
        };
        var outboxMigration = new ContactCenterOutboxMessageIndexMigrations(
            store,
            new StubClock(_erasedUtc))
        {
            SchemaBuilder = schemaBuilder,
        };

        await interactionMigration.CreateAsync();
        await interactionMigration.UpdateFrom1Async();
        await interactionMigration.UpdateFrom2Async();
        await interactionMigration.UpdateFrom3Async();
        await interactionMigration.UpdateFrom4Async();
        await interactionMigration.UpdateFrom5Async();
        await callSessionMigration.CreateAsync();
        await eventMigration.CreateAsync();
        await eventMigration.UpdateFrom2Async();
        await outboxMigration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
