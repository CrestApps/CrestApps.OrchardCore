using CrestApps.Core.ContactCenter;
using CrestApps.Core.Locking;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.Core.Telephony.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.Core.Data.YesSql.ContactCenter.Services;
using CrestApps.Core.Data.YesSql.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderWebhookInboxPersistenceTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AcceptAsync_CommitsBeforeReturn_AndDuplicateIsVisibleToANewSession()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-webhook-inbox-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new ProviderWebhookInboxMessageIndexProvider(new ProviderIdentityResolver([]))]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);
        await CreateIndexSchemaAsync(store);

        try
        {
            var distributedLock = CreateDistributedLock();
            ProviderWebhookInboxAcceptanceResult firstResult;

            await using (var firstSession = store.CreateSession())
            {
                var inbox = CreateInbox(firstSession, distributedLock);
                firstResult = await inbox.AcceptAsync(CreateDelivery(), TestContext.Current.CancellationToken);
            }

            await using var secondSession = store.CreateSession();
            var secondInbox = CreateInbox(secondSession, distributedLock);

            // Act
            var duplicateResult = await secondInbox.AcceptAsync(CreateDelivery(), TestContext.Current.CancellationToken);
            var persisted = await new ProviderWebhookInboxStore(secondSession).FindByDeliveryAsync(
                "provider",
                "delivery-1",
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderWebhookInboxAcceptanceStatus.Accepted, firstResult.Status);
            Assert.Equal(ProviderWebhookInboxAcceptanceStatus.Duplicate, duplicateResult.Status);
            Assert.Equal(firstResult.MessageId, duplicateResult.MessageId);
            Assert.NotNull(persisted);
            Assert.Equal(firstResult.MessageId, persisted.ItemId);
            Assert.Equal(ProviderWebhookInboxStatus.Pending, persisted.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task CreateIndexSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(100))
            .Column<string>("DeliveryId", column => column.WithLength(256))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull())
            .Column<DateTime?>("ProcessedUtc"),
            collection: ContactCenterStorage.CollectionName);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static ProviderWebhookInbox CreateInbox(ISession session, IDistributedLockProvider distributedLock)
    {
        var clock = new FakeTimeProvider();
        clock.SetUtcNow(_now);

        return new ProviderWebhookInbox(
            [],
            new ProviderWebhookInboxStore(session),
            new YesSqlStoreCommitter(session, NullLogger<YesSqlStoreCommitter>.Instance),
            distributedLock,
            new ProviderIdentityResolver([]),
            new Mock<IContactCenterScopeExecutor>().Object,
            clock,
            Options.Create(new ContactCenterRetentionOptions()),
            NullLogger<ProviderWebhookInbox>.Instance);
    }

    private static IDistributedLockProvider CreateDistributedLock()
    {
        var distributedLock = new Mock<IDistributedLockProvider>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, true));

        return distributedLock.Object;
    }

    private static ProviderWebhookInboxDelivery CreateDelivery()
    {
        return new ProviderWebhookInboxDelivery
        {
            ProviderName = "provider",
            DeliveryId = "delivery-1",
            HandlerName = "handler",
            Payload = "{}",
        };
    }
}
