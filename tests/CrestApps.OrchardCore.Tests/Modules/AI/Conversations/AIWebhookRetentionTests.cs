using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

/// <summary>
/// Bug: AI webhook deliveries were never purged, so the delivery table grew forever. Settled deliveries older than the
/// retention window are now deleted, as commerce webhook deliveries are; pending ones are kept so none is lost.
/// </summary>
public sealed class AIWebhookRetentionTests
{
    private static readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DeleteOlderThanAsync_DeletesOnlySettledDeliveriesOlderThanTheCutoff()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"ai-webhook-retention-{Guid.NewGuid():N}.db");
        var options = Options.Create(new YesSqlStoreOptions());
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new AIWebhookDeliveryIndexProvider(options)]);

        try
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.InitializeCollectionAsync(options.Value.AICollectionName, TestContext.Current.CancellationToken);

            await using (var session = store.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                await AIWebhookIndexSchema.CreateAsync(new SchemaBuilder(store.Configuration, transaction), options.Value);
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                var deliveries = new AIWebhookDeliveryStore(session, options);

                await deliveries.CreateAsync(Delivery("old-delivered", AIWebhookDeliveryStatus.Delivered, _now.AddDays(-31)), TestContext.Current.CancellationToken);
                await deliveries.CreateAsync(Delivery("old-failed", AIWebhookDeliveryStatus.Failed, _now.AddDays(-45)), TestContext.Current.CancellationToken);
                await deliveries.CreateAsync(Delivery("old-pending", AIWebhookDeliveryStatus.Pending, _now.AddDays(-40)), TestContext.Current.CancellationToken);
                await deliveries.CreateAsync(Delivery("recent-delivered", AIWebhookDeliveryStatus.Delivered, _now.AddDays(-2)), TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            int deleted;

            await using (var session = store.CreateSession())
            {
                deleted = await new AIWebhookDeliveryStore(session, options).DeleteOlderThanAsync(_now.AddDays(-30), 100, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            await using (var session = store.CreateSession())
            {
                var remaining = (await new AIWebhookDeliveryStore(session, options).GetAllAsync(TestContext.Current.CancellationToken))
                    .Select(delivery => delivery.ItemId)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(2, deleted);
                Assert.Equal(["old-pending", "recent-delivered"], remaining);
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static AIWebhookDelivery Delivery(string id, AIWebhookDeliveryStatus status, DateTime createdUtc)
        => new()
        {
            ItemId = id,
            SubscriptionId = "subscription-1",
            EventName = "conversation.started",
            Status = status,
            CreatedUtc = createdUtc,
            NextAttemptUtc = createdUtc,
        };
}
