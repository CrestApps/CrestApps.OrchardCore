using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

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
        store.RegisterIndexes([new ProviderWebhookInboxMessageIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);
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
            store.Dispose();
            File.Delete(databasePath);
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
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static ProviderWebhookInbox CreateInbox(ISession session, IDistributedLock distributedLock)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return new ProviderWebhookInbox(
            [],
            new ProviderWebhookInboxStore(session),
            session,
            distributedLock,
            clock.Object,
            NullLogger<ProviderWebhookInbox>.Instance);
    }

    private static IDistributedLock CreateDistributedLock()
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
