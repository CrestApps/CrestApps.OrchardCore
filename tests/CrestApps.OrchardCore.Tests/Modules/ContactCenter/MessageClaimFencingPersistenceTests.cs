using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class MessageClaimFencingPersistenceTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OutboxCompletion_AfterNewerClaimCommitted_FailsAndPreservesNewOwner()
    {
        var databasePath = DatabasePath("outbox-fence");
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new ContactCenterOutboxMessageIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            ContactCenterConstants.CollectionName,
            TestContext.Current.CancellationToken);
        await CreateOutboxSchemaAsync(store);

        try
        {
            await SeedOutboxAsync(store);
            await using var sessionA = store.CreateSession();
            var storeA = new ContactCenterOutboxStore(sessionA);
            var messageA = await storeA.FindByIdAsync("message-1", TestContext.Current.CancellationToken);
            messageA.Status = OutboxMessageStatus.Claimed;
            messageA.OwnerToken = "worker-a";
            messageA.FenceToken = 1;
            messageA.NextAttemptUtc = _now.AddMinutes(5);
            await storeA.UpdateAsync(messageA, TestContext.Current.CancellationToken);
            await sessionA.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var sessionB = store.CreateSession();
            var storeB = new ContactCenterOutboxStore(sessionB);
            var messageB = await storeB.FindByIdAsync("message-1", TestContext.Current.CancellationToken);
            messageB.OwnerToken = "worker-b";
            messageB.FenceToken = 2;
            messageB.NextAttemptUtc = _now.AddMinutes(10);
            await storeB.UpdateAsync(messageB, TestContext.Current.CancellationToken);
            await sessionB.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            await using var settlementSession = store.CreateSession();
            var settlement = new ContactCenterOutbox(
                [],
                new ContactCenterOutboxStore(settlementSession),
                new Mock<IInteractionEventStore>().Object,
                new Mock<IContactCenterScopeExecutor>().Object,
                new TestContactCenterFeatureWorkManager(),
                settlementSession,
                CreateClock(),
                NullLogger<ContactCenterOutbox>.Instance);
            var exception = await Record.ExceptionAsync(() => settlement.SettleClaimAsync(
                "message-1",
                "worker-a",
                1,
                [],
                null,
                false,
                TestContext.Current.CancellationToken));

            // Assert
            AssertConcurrencyFailure(exception);
            await using var verificationSession = store.CreateSession();
            var persisted = await new ContactCenterOutboxStore(verificationSession)
                .FindByIdAsync("message-1", TestContext.Current.CancellationToken);
            Assert.Equal(OutboxMessageStatus.Claimed, persisted.Status);
            Assert.Equal("worker-b", persisted.OwnerToken);
            Assert.Equal(2, persisted.FenceToken);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task InboxCompletion_AfterNewerClaimCommitted_FailsAndPreservesNewOwner()
    {
        var databasePath = DatabasePath("inbox-fence");
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([
            new ProviderWebhookInboxMessageIndexProvider(new ProviderIdentityResolver([])),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            ContactCenterConstants.CollectionName,
            TestContext.Current.CancellationToken);
        await CreateInboxSchemaAsync(store);

        try
        {
            await SeedInboxAsync(store);
            await using var sessionA = store.CreateSession();
            var storeA = new ProviderWebhookInboxStore(sessionA);
            var messageA = await storeA.FindByIdAsync("message-1", TestContext.Current.CancellationToken);
            messageA.Status = ProviderWebhookInboxStatus.Claimed;
            messageA.OwnerToken = "worker-a";
            messageA.FenceToken = 1;
            messageA.NextAttemptUtc = _now.AddMinutes(5);
            await storeA.UpdateAsync(messageA, TestContext.Current.CancellationToken);
            await sessionA.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var sessionB = store.CreateSession();
            var storeB = new ProviderWebhookInboxStore(sessionB);
            var messageB = await storeB.FindByIdAsync("message-1", TestContext.Current.CancellationToken);
            messageB.OwnerToken = "worker-b";
            messageB.FenceToken = 2;
            messageB.NextAttemptUtc = _now.AddMinutes(10);
            await storeB.UpdateAsync(messageB, TestContext.Current.CancellationToken);
            await sessionB.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            await using var settlementSession = store.CreateSession();
            var settlement = new ProviderWebhookInbox(
                [],
                new ProviderWebhookInboxStore(settlementSession),
                settlementSession,
                new Mock<IDistributedLock>().Object,
                new ProviderIdentityResolver([]),
                new Mock<IContactCenterScopeExecutor>().Object,
                CreateClock(),
                NullLogger<ProviderWebhookInbox>.Instance);
            var exception = await Record.ExceptionAsync(() => settlement.SettleClaimAsync(
                "message-1",
                "worker-a",
                1,
                true,
                null,
                TestContext.Current.CancellationToken));

            // Assert
            AssertConcurrencyFailure(exception);
            await using var verificationSession = store.CreateSession();
            var persisted = await new ProviderWebhookInboxStore(verificationSession)
                .FindByIdAsync("message-1", TestContext.Current.CancellationToken);
            Assert.Equal(ProviderWebhookInboxStatus.Claimed, persisted.Status);
            Assert.Equal("worker-b", persisted.OwnerToken);
            Assert.Equal(2, persisted.FenceToken);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static void AssertConcurrencyFailure(Exception exception)
    {
        Assert.True(
            exception is ConcurrencyException or DbException,
            $"Expected an optimistic-concurrency failure but received {exception?.GetType().Name ?? "no exception"}.");
    }

    private static string DatabasePath(string prefix)
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"contact-center-{prefix}-{Guid.NewGuid():N}.db");
    }

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static async Task CreateOutboxSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        await schemaBuilder.CreateMapIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EventId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task CreateInboxSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        await schemaBuilder.CreateMapIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(100))
            .Column<string>("DeliveryId", column => column.WithLength(256))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SeedOutboxAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var outboxStore = new ContactCenterOutboxStore(session);
        await outboxStore.CreateAsync(new ContactCenterOutboxMessage
        {
            ItemId = "message-1",
            EventId = "event-1",
            Status = OutboxMessageStatus.Pending,
            NextAttemptUtc = _now,
            CreatedUtc = _now,
        }, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SeedInboxAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var inboxStore = new ProviderWebhookInboxStore(session);
        await inboxStore.CreateAsync(new ProviderWebhookInboxMessage
        {
            ItemId = "message-1",
            ProviderName = "provider",
            DeliveryId = "delivery-1",
            HandlerName = "handler",
            Payload = "{}",
            Status = ProviderWebhookInboxStatus.Pending,
            NextAttemptUtc = _now,
            CreatedUtc = _now,
        }, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
