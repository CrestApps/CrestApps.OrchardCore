using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Webhooks.BackgroundTasks;
using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Core.Indexes;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks;

/// <summary>
/// The AI webhook delivery log was never deleted and admins could read it. Moving AI events to the shared Webhooks
/// building block must keep that: the whole history is copied to the shared Deliveries page, the old rows stay, and
/// AI deliveries are not purged by default, while other deliveries keep their 30-day retention.
/// </summary>
public sealed class AIWebhookDeliveryHistoryTests
{
    private static readonly DateTime _now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ImportHistory_CopiesDeliveredAndFailedDeliveries_AndKeepsTheLegacyRows()
    {
        // Arrange
        var delivered = Legacy("ai-delivered", AIWebhookDeliveryStatus.Delivered, _now.AddDays(-90), statusCode: 200);
        var failed = Legacy("ai-failed", AIWebhookDeliveryStatus.Failed, _now.AddDays(-60), statusCode: 500, error: "Server error");
        var moved = Legacy("ai-moved", AIWebhookDeliveryStatus.Failed, _now.AddDays(-1), error: AIWebhookImporter.MovedError);

        var aiDeliveries = new Mock<IAIWebhookDeliveryStore>();
        aiDeliveries.Setup(store => store.ListSettledAsync(0, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([delivered, failed, moved]);
        aiDeliveries.Setup(store => store.ListSettledAsync(It.Is<int>(skip => skip > 0), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var created = new List<WebhookDelivery>();
        var deliveries = new Mock<IWebhookDeliveryStore>();
        deliveries.Setup(store => store.CreateAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookDelivery, CancellationToken>((delivery, _) => created.Add(delivery))
            .Returns(ValueTask.CompletedTask);

        var importer = CreateImporter(aiDeliveries.Object, deliveries.Object);

        // Act
        var copied = await importer.ImportHistoryAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, copied);
        Assert.Equal(["ai-delivered", "ai-failed"], created.Select(delivery => delivery.EventId));

        var copiedDelivered = created[0];
        Assert.Equal(WebhookDeliveryStatus.Delivered, copiedDelivered.Status);
        Assert.Equal("ai-sub-1", copiedDelivered.SubscriptionId);
        Assert.Equal(AIWebhookEvents.ConversationEnded, copiedDelivered.EventName);
        Assert.Equal(200, copiedDelivered.LastStatusCode);
        Assert.Equal(_now.AddDays(-90), copiedDelivered.CreatedUtc);
        Assert.Equal(_now.AddDays(-90).AddMinutes(1), copiedDelivered.DeliveredUtc);
        Assert.Null(copiedDelivered.NextAttemptUtc);

        var copiedFailed = created[1];
        Assert.Equal(WebhookDeliveryStatus.Failed, copiedFailed.Status);
        Assert.Equal("Server error", copiedFailed.LastError);
        Assert.Null(copiedFailed.DeliveredUtc);

        // The legacy rows are neither changed nor deleted.
        aiDeliveries.Verify(store => store.UpdateAsync(It.IsAny<AIWebhookDelivery>(), It.IsAny<CancellationToken>()), Times.Never);
        aiDeliveries.Verify(store => store.DeleteAsync(It.IsAny<AIWebhookDelivery>(), It.IsAny<CancellationToken>()), Times.Never);
        aiDeliveries.Verify(store => store.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ThePurge_SparesAIDeliveries_AndKeepsTheThirtyDayRetentionForTheOthers()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"webhook-retention-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new WebhookDeliveryIndexProvider()]);

        try
        {
            await CreateSchemaAsync(store);

            await using (var session = store.CreateSession())
            {
                var deliveries = new WebhookDeliveryStore(session);

                await deliveries.CreateAsync(Shared("ai-old", AIWebhookEvents.ConversationEnded, _now.AddDays(-400)), TestContext.Current.CancellationToken);
                await deliveries.CreateAsync(Shared("ai-alert-old", AIWebhookEvents.AlertTriggered, _now.AddDays(-45)), TestContext.Current.CancellationToken);
                await deliveries.CreateAsync(Shared("order-old", "order.paid", _now.AddDays(-45)), TestContext.Current.CancellationToken);
                await deliveries.CreateAsync(Shared("order-recent", "order.paid", _now.AddDays(-2)), TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var options = new WebhookDeliveryRetentionOptions();
            ConfigureAIWebhooks(options);

            // Act
            await using (var session = store.CreateSession())
            {
                var services = new ServiceCollection()
                    .AddSingleton(Clock())
                    .AddSingleton<ISession>(session)
                    .AddSingleton<IWebhookDeliveryStore>(new WebhookDeliveryStore(session))
                    .AddSingleton(new WebhookDispatcher(Mock.Of<CrestApps.OrchardCore.Webhooks.IWebhookSender>(), Mock.Of<IWebhookDeliveryStore>(), Mock.Of<IWebhookSubscriptionStore>(), new EphemeralDataProtectionProvider(), Clock(), NullLogger<WebhookDispatcher>.Instance))
                    .AddSingleton(Options.Create(options))
                    .BuildServiceProvider();

                await new WebhookDeliveriesBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            await using (var session = store.CreateSession())
            {
                var remaining = (await new WebhookDeliveryStore(session).GetAllAsync(TestContext.Current.CancellationToken))
                    .Select(delivery => delivery.ItemId)
                    .Order(StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(["ai-alert-old", "ai-old", "order-recent"], remaining);
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public void TheAIWebhooksFeature_KeepsEveryAIEvent_AndNoLongerPurgesTheLegacyLog()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new WebhooksStartup().ConfigureServices(services);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<WebhookDeliveryRetentionOptions>>().Value;

        // Assert
        Assert.All(AIWebhookEvents.All, name => Assert.Contains(name, options.KeptEventNames));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IBackgroundTask) &&
            descriptor.ImplementationType?.Namespace?.StartsWith("CrestApps.OrchardCore.AI", StringComparison.Ordinal) == true);
    }

    private static void ConfigureAIWebhooks(WebhookDeliveryRetentionOptions options)
    {
        var services = new ServiceCollection();
        new WebhooksStartup().ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        foreach (var name in provider.GetRequiredService<IOptions<WebhookDeliveryRetentionOptions>>().Value.KeptEventNames)
        {
            options.KeptEventNames.Add(name);
        }
    }

    private static async Task CreateSchemaAsync(IStore store)
    {
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(WebhookStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<WebhookDeliveryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SubscriptionId", column => column.WithLength(26))
            .Column<string>("EventName", column => column.WithLength(64))
            .Column<WebhookDeliveryStatus>("Status")
            .Column<DateTime>("NextAttemptUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: WebhookStorage.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static AIWebhookImporter CreateImporter(IAIWebhookDeliveryStore aiDeliveries, IWebhookDeliveryStore deliveries)
    {
        var protection = new EphemeralDataProtectionProvider();
        var subscriptions = Mock.Of<IWebhookSubscriptionStore>();
        var dispatcher = new WebhookDispatcher(Mock.Of<CrestApps.OrchardCore.Webhooks.IWebhookSender>(), deliveries, subscriptions, protection, Clock(), NullLogger<WebhookDispatcher>.Instance);

        return new AIWebhookImporter(Mock.Of<IAIWebhookSubscriptionStore>(), aiDeliveries, subscriptions, deliveries, dispatcher, protection, Clock(), NullLogger<AIWebhookImporter>.Instance, Mock.Of<ISession>());
    }

    private static AIWebhookDelivery Legacy(string id, AIWebhookDeliveryStatus status, DateTime createdUtc, int? statusCode = null, string error = null)
        => new()
        {
            ItemId = id,
            SubscriptionId = "ai-sub-1",
            EventName = AIWebhookEvents.ConversationEnded,
            Payload = $"{{\"id\":\"{id}\"}}",
            Status = status,
            Attempts = 1,
            StatusCode = statusCode,
            Error = error,
            CreatedUtc = createdUtc,
            NextAttemptUtc = createdUtc,
            CompletedUtc = createdUtc.AddMinutes(1),
        };

    private static WebhookDelivery Shared(string id, string eventName, DateTime createdUtc)
        => new()
        {
            ItemId = id,
            SubscriptionId = "sub-1",
            EventId = id,
            EventName = eventName,
            Payload = "{}",
            Status = WebhookDeliveryStatus.Delivered,
            CreatedUtc = createdUtc,
            DeliveredUtc = createdUtc,
        };

    private static IClock Clock()
        => Mock.Of<IClock>(clock => clock.UtcNow == _now);
}
